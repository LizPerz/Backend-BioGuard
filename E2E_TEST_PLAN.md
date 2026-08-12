# Plan de Pruebas End-to-End - Sistema ML BioGuard

## Objetivo
Verificar flujo completo: Wear OS → Mobile (Local F1-F3) → Backend (Persist) → Web (Display)

---

## Test 1: Cálculo Local en Mobile (GlycemicPeakPredictor)

### Entrada
- Pulso: 75 BPM
- Temperatura: 36.5°C
- Sudoración: 25 µS
- Peso: 70 kg
- Altura: 180 cm

### Esperado
- F1 (IMC): 21.6
- F2 (Z-score): < 0.5
- F3 (P(Pico)): < 0.5
- Nivel: "Óptimo"

### Paso 1: Ejecutar cálculo
```kotlin
val predictor = GlycemicPeakPredictor()
val resultado = predictor.calcularPrediccion(75, 36.5, 25, 70, 180)
println(resultado) // Verificar valores
```

---

## Test 2: Sincronización Offline-Online (Mobile)

### Paso 1: Desactivar red
- Settings → Airplane Mode ON

### Paso 2: Generar reporte
- Dashboard → "Reporte Glucémico"
- Verificar error de red
- Revisar que se guardó en `pending_predictions_ml` table

### Paso 3: Reactivar red
- Settings → Airplane Mode OFF

### Paso 4: Sincronizar
- Dashboard → "Sincronizar Datos"
- Verificar que se envían reportes pendientes
- Revisar logs de `PredictionMlSyncWorker`

### Esperado
- Datos pendientes se sincronizan automáticamente cada 15 min
- O manualmente con botón "Sincronizar"

---

## Test 3: Backend Endpoints (Postman)

### Setup
```
Authorization: Bearer {PACIENTE_TOKEN}
Content-Type: application/json
```

### Test 3A: POST /api/Sensores/prediccion

**Request:**
```json
{
  "probabilidadPico": 0.82,
  "nivelRiesgo": "Moderado Alto",
  "casoClinico": "Hiperglucemia Severa",
  "imc": 26.0,
  "z": 1.8,
  "pPico": 0.82,
  "recomendacion": "Reducir ingesta de carbohidratos"
}
```

**Expected Response:**
```json
{
  "prediccionId": "{ID}",
  "message": "Predicción guardada correctamente"
}
```

**Verificar:**
- Status: 200 OK
- MongoDB: colección `predicciones_ml` tiene nuevo documento
- Auditoría: registrado en `auditoria`

### Test 3B: GET /api/Sensores/predicciones/{pacienteId}

**Expected Response:**
```json
[
  {
    "id": "{ID}",
    "pacienteId": "{PACIENTE_ID}",
    "probabilidadPico": 0.82,
    "nivelRiesgo": "Moderado Alto",
    "casoClinico": "Hiperglucemia Severa",
    "imc": 26.0,
    "z": 1.8,
    "pPico": 0.82,
    "fechaPrediccion": "2026-08-12T15:30:00Z"
  }
]
```

**Verificar:**
- Status: 200 OK
- Array ordenado descendente por fechaPrediccion
- Máximo 50 registros

### Test 3C: GET /api/Sensores/predicciones/{pacienteId}/actual

**Expected Response:**
```json
{
  "id": "{ID}",
  "pacienteId": "{PACIENTE_ID}",
  "probabilidadPico": 0.82,
  ...
  "fechaPrediccion": "2026-08-12T15:30:00Z"
}
```

**Verificar:**
- Status: 200 OK
- Retorna predicción más reciente
- Null si no hay historial

### Test 3D: Webhook Crítico (P ≥ 0.75)

**Request con P = 0.95:**
```json
{
  "probabilidadPico": 0.95,
  "nivelRiesgo": "Crítico Alto",
  "casoClinico": "Hipoglucemia Nocturna",
  ...
}
```

**Verificar:**
- Status: 200 OK
- MongoDB: documento en `notificaciones_ml_eventos`
- Field: `estadoEnvio` = "SENT" o "PENDING"
- Logs: NotificacionMlService registró evento

---

## Test 4: Web Visualization (React)

### Setup
- Navegar a `/reportes`
- Seleccionar paciente con predicciones ML

### Test 4A: PrediccionMlCard Rendering

**Verificar:**
- Componente se renderiza sin errores
- Muestra IMC, Z-score, P(Pico)
- Barra de progreso coloreada:
  - Verde (Bajo): P < 0.5
  - Amarillo (Moderado): 0.5 ≤ P < 0.75
  - Rojo (Crítico): P ≥ 0.75
- Caso clínico visible

### Test 4B: Grid Responsivo

**Desktop (1920x1080):**
- Grid de 3 columnas
- Cards con espaciado 16px

**Tablet (768x1024):**
- Grid de 2 columnas
- Layouts se ajustan correctamente

**Mobile (375x667):**
- Grid de 1 columna
- Scrollable verticalmente

### Test 4C: API Fetch

**Console logs:**
```
✓ getPredicciones() retorna array
✓ Campos completos: id, pacienteId, probabilidadPico, etc.
✓ Timestamps válidos
```

---

## Test 5: Matriz de Riesgo (Mobile)

### Casos de Prueba

| Pulso | Temp | Sudor | Caso | Nivel | P(Pico) |
|-------|------|-------|------|-------|---------|
| 115   | 34.5 | 85    | Hipoglucemia Nocturna | Crítico Alto | > 0.75 |
| 100   | 37.5 | 25    | Higerglucemia Severa | Moderado Alto | 0.5-0.75 |
| 70    | 36.5 | 25    | Óptimo | Bajo | < 0.5 |

**Verificar para cada caso:**
- GlycemicPeakPredictor calcula correctamente
- NivelRiesgo coincide
- CasoClinico es el esperado

---

## Test 6: Integración Completa (Flujo End-to-End)

### Paso 1: Generar en Mobile
```
1. Abrir DashboardScreen
2. Ingresar vitales: 75 BPM, 36.5°C, 25 µS
3. Presionar "Reporte Glucémico"
4. Verificar: local cache + UI spinner
```

### Paso 2: Enviar al Backend
```
5. Conectar a internet
6. Presionar "Sincronizar Datos"
7. Verificar: POST a /api/Sensores/prediccion
8. Verificar: respuesta 200 OK
```

### Paso 3: Verificar en Backend
```
9. MongoDB: documento en predicciones_ml
10. Auditoría: entrada en auditoria
11. Si P >= 0.75: evento en notificaciones_ml_eventos
```

### Paso 4: Visualizar en Web
```
12. Ir a /reportes
13. Seleccionar paciente
14. Verificar: PrediccionMlCard renderiza predicción
15. Verificar: datos coinciden con Mobile + Backend
```

---

## Criterios de Aceptación

✅ **DEBE PASAR:**
- Mobile calcula F1-F3 localmente (offline)
- Backend persiste predicciones correctamente
- Web visualiza predicciones sin errores
- Webhooks se disparan para P ≥ 0.75
- Sincronización offline/online funciona
- Todos los endpoints retornan 200 OK
- Datos são consistentes en toda la cadena

❌ **NO DEBE OCURRIR:**
- Pérdida de datos al sincronizar
- Cálculos ML incorrectos
- Falta de notificaciones críticas
- Errores en web al renderizar
- Duplicados al re-sincronizar

---

## Datos de Prueba

```
Paciente ID: 123456789012345678901234
Peso: 70 kg
Altura: 180 cm
Pesos ML (F2): w0=-8.0, w1=0.05, w2=0.02, w3=-0.04, w4=0.15
Umbral Crítico: P(Pico) >= 0.75
```

---

## Resultados

| Test | Status | Notas |
|------|--------|-------|
| 1. Cálculo Local | ⏳ | Verificar en Kotlin |
| 2. Sync Offline | ⏳ | Verificar PredictionMlSyncWorker |
| 3. Backend Endpoints | ⏳ | Postman request |
| 4. Web Visualization | ⏳ | Chrome DevTools |
| 5. Matriz Riesgo | ⏳ | Unit test |
| 6. E2E Completo | ⏳ | Manual flow |

---

**Fecha:** 2026-08-12  
**Versión:** 1.0  
**Estado:** Pendiente ejecución manual
