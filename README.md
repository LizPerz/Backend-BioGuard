# BioGuard API - Backend

API RESTful para el ecosistema médico IoT **BioGuard**. Gestiona pacientes con enfermedades metabólicas (diabetes, hipertensión), dispositivos WearOS, lecturas de sensores en tiempo real, alertas críticas, predicciones ML y reportes clínicos.

## Arquitectura General

```
                    +-------------------+
                    |   React/Next.js   |  Dashboard Web
                    |   (Web Repo)      |
                    +--------+----------+
                             |
                    +--------v----------+
|   .NET 9 API      | Backend (este repositorio)
|   94 endpoints    |
                    +--------+----------+
                             |
              +--------------+--------------+
              |              |              |
    +---------v---+  +------+--------+  +--+-----------+
    |  Kotlin App  |  |  Wear OS     |  |  Python ML   |
    |  (Móvil)     |  |  (WearOS)    |  |  (ML)        |
    |  BLE + SQLite|  |  BLE only    |  |  FastAPI     |
    +--------------+  +--------------+  +--------------+
              |              |              |
              +--------------+--------------+
                             |
                    +--------v----------+
                    |   MongoDB Atlas   |  Base de datos
                    |   18 colecciones  |
                    +-------------------+
```

## Repositorios

| Repositorio | Tecnología | Descripción |
|---|---|---|
| **Api-BioGuard** | .NET 9 / C# | Backend API RESTful (este repo) |
| **Móvil** | Kotlin / Android | App móvil paciente + cuidador |
| **Web** | React / Next.js | Dashboard web para cuidadores |
| **WearOS** | Kotlin / Wear OS | App para reloj WearOS |
| **ML** | Python / FastAPI | Modelo de predicciones ML |

## Stack Tecnológico

| Capa | Tecnología | Versión |
|---|---|---|
| Runtime | .NET | 9.0 |
| Lenguaje | C# | 13 |
| Base de datos | MongoDB Atlas | 7.0+ |
| MongoDB Driver | MongoDB.Driver | 3.10.0 |
| Auth | JWT + PBKDF2 (600K iteraciones) | |
| Tiempo real | SignalR | |
| Email | MailKit | 4.17.0 |
| Rate Limiting | AspNetCoreRateLimit | 5.0.0 |
| Container | Docker | Multi-stage |
| CI/CD | GitHub Actions | |
| Deploy | DigitalOcean App Platform | |
| API Docs | Swagger / OpenAPI | |
| Tests | xUnit + FluentAssertions | 511 tests |

## Funcionalidades

### Módulo 1: Autenticación y Usuarios
- Registro con verificación por email (código 6-dígitos)
- Login web con JWT + Refresh Token rotation
- Login por Google OAuth
- Login por código QR (cuidador) — regeneración automática si ya existe
- 2FA por correo electrónico
- Recuperación de password por email
- Bloqueo de cuenta (5 intentos fallidos = 15 min lockout)
- Logout con revocación de token (blacklist)
- **Migración de passwords**: endpoint `/api/Seed/migrate-passwords` que convierte hashes BCrypt a PBKDF2 (600K iteraciones)

### Módulo 2: Pacientes
- CRUD completo de pacientes
- 1 usuario_web = 1 paciente (máximo)
- Edad calculada automáticamente desde fecha de nacimiento
- Datos biométricos del onboarding móvil: fecha de nacimiento, peso (kg), estatura (cm), sexo, actividad física, diabetes (paciente y familiares)
- Alta con datos completos en un solo `POST /api/Pacientes` (retrocompatible: solo `{ nombre }` sigue funcionando)
- `PUT /api/Pacientes/{id}/biometria` para guardar/editar biometría (incluye fecha de nacimiento y sexo)
- Perfil con código QR (`codigoAccesoQr`) devuelto en `GET mi-paciente`
- Foto de perfil

### Módulo 3: Cuidadores y Dispositivos
- CRUD de cuidadores (N por plan)
- Vinculación de dispositivos WearOS (MAC address)
- Heartbeat / keepalive del reloj
- Conexiones BLE reloj -> teléfono -> API

### Módulo 4: Sensores y Lecturas
- Recepción de lecturas: glucosa, presión, pulso, temperatura, SpO2, peso, GSR
- Batch upload (SQLite offline -> API, max 10MB)
- Estadísticas, tendencias y resumen
- Tracking GPS con historial de ruta
- Eventos metabólicos con atención médica

### Módulo 5: Alertas
- Alertas críticas (hipoglucemia, hiperglucemia, taquicardia, etc.)
- Creación automática por sensores/ML
- Resolución con acción tomada
- Notificaciones push (FCM)

### Módulo 6: Medicamentos
- CRUD de medicamentos por paciente
- Registro de tomas
- Trigger automático desde ML cuando detecta pico crítico

### Módulo 7: Reportes
- Resumen general del paciente
- Historial de alertas, eventos, medicamentos y lecturas
- Exportar lecturas a CSV

### Módulo 8: Machine Learning
- Predicciones de riesgo metabólico
- Recomendaciones personalizadas
- Entrenamiento y re-entrenamiento de modelos
- Diagnósticos puntuales
- Métricas de modelos

### Módulo 9: Pagos y Planes
- 3 planes: Gratis ($0), Familiar ($1 MXN), Pro ($2 MXN) — suscripciones mensuales
- **Pagos reales con Stripe**: `POST /api/Pagos/crear-sesion` crea la sesión de checkout y devuelve `checkoutUrl`. **Importante:** el frontend debe redirigir a `checkoutUrl` tal cual — la URL de Stripe lleva un fragmento cifrado que no se puede reconstruir con el `sessionId`
- Confirmación vía **webhook seguro** de Stripe (firma HMAC, tolerante a `api_version`, idempotente, sin exponer secretos)
- Webhook: `POST /api/Pagos/webhook/stripe` (solo lo invoca Stripe, no requiere JWT)
- Solo `metodoPago: "stripe"` (Mercado Pago eliminado); cualquier otro valor responde 400
- `plan.StripePriceId` (`stripe_price_id` en MongoDB) define el precio de Stripe; si falta, se crea automáticamente
- Historial de pagos y recibos
- Cancelación de suscripción activa

### Módulo 10: Web Dashboard
- Perfil de usuario con edición
- Cambio de plan
- Gestión de correo electrónico
- Eliminación de cuenta

### Módulo 11: Auditoría
- Log de actividades: login, creación, actualización, alertas
- IP y timestamp por acción

## Seguridad

### Autenticación y Autorización
- **JWT** con roles: `dueno`, `paciente`, `cuidador`, `admin`
- **PBKDF2** password hashing (600,000 iteraciones, SHA256, 16-byte salt, 32-byte key) — reemplaza BCrypt obsoleto
- **Refresh Token rotation**: cada uso genera un nuevo token y revoca el anterior
- **Token Blacklist**: logout revoca el JTI del JWT
- **Account lockout**: 5 intentos fallidos -> 15 minutos de bloqueo
- **Password complexity**: min 8 chars, mayúscula, minúscula, dígito, caracter especial
- **2FA**: código de 6 dígitos con expiración temporal
- **Timing-safe comparison**: `CryptographicOperations.FixedTimeEquals` para 2FA y passwords

### Protección IDOR (OWASP Top 10 #1)
- `OwnershipHelper` verifica que el usuario autenticado sea dueño del paciente en **TODOS** los endpoints protegidos (14 controladores)
- Cuidadores solo acceden a pacientes vinculados (verificación en DB contra colección `cuidadores`)
- Endpoint `Seed` restringido a `admin`

### Endpoints auditados con OwnershipHelper
| Controlador | Endpoints protegidos |
|---|---|
| `PacientesController` | GetById, Update, Delete |
| `SensoresController` | Lecturas, Estadísticas, Eventos, Tracking, Reportes |
| `AlertasController` | GetByPaciente, Pendientes, GetById, Crear, Resolver, Delete |
| `MedicamentosController` | GetByPaciente, GetById, Update, RegistrarToma, CambiarActivo, Delete |
| `CuidadoresController` | GetByPaciente, Crear, Update, Delete |
| `NotificacionesController` | GetByPaciente, Crear, MarcarLeida, Eliminar |
| `ReportesController` | Resumen, HistorialAlertas, HistorialEventos, HistorialMedicamentos, HistorialLecturas |
| `MLController` | ObtenerPredicciones, PrediccionActual, Recomendaciones, Diagnosticar |
| `PagosController` | Recibo (verificación de propietario del pago) |
| `DispositivosController` | GetByPaciente, Update, Desvincular |

### Endpoints con token `paciente_id` (para WearOS)
- `POST /api/Sensores/lectura`, `POST /api/Sensores/evento`, `POST /api/Sensores/tracking`
- `POST /api/Dispositivos/vincular`, `POST /api/Dispositivos/heartbeat`

### Headers de Seguridad
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy` (geolocation, camera, microphone deshabilitados)
- `Strict-Transport-Security: max-age=31536000`
- `Content-Security-Policy: default-src 'self'; frame-ancestors 'none'`
- `X-Powered-By` eliminado

### Rate Limiting

| Endpoint | Límite |
|---|---|
| General | 100 req/min |
| POST general | 30 req/min |
| Login | 5 req/min |
| Register | 3 req/min |
| 2FA enviar | 3 req/min |
| 2FA verificar | 5 req/min |
| Forgot password | 3 req/min |
| Refresh token | 10 req/min |
| Reset password | 3 req/min |
| Cambiar password (PUT) | 3 req/min |
| Sensores/lectura | 60 req/min |
| Sensores/lectura-batch | 10 req/min |

### Validación de Entrada
- DTOs con `[Required]`, `[StringLength]`, `[Range]`, `[EmailAddress]`, `[MinLength]`
- Request size limits: SubirFoto 1MB, Batch 10MB
- Global exception handler (catch-all -> JSON con traceId)

### Otras mejoras de seguridad
- **MAC address masking**: Las MAC de dispositivos WearOS se muestran como `XX:XX:XX:XX:XX:XX` en respuestas JSON
- **Datos sensibles eliminados de logs**: códigos de acceso, passwords, valores de código QR ya no se loggean
- **Claim estandarizado**: Todos los controladores usan `ClaimTypes.NameIdentifier` (no `"sub"`) vía `User.FindFirst()`
- **DAST Scan**: Workflow de GitHub Actions con OWASP ZAP para análisis dinámico de seguridad (corre semanalmente y en PRs a master)
- **Código QR con expiración**: El código de acceso QR de paciente/cuidador expira a los 10 minutos (campo `CodigoExpira`), validado en login y auto-vinculación
- **Security Gate**: CodeQL con `codeql-config.yml` (ignora tests, excluye `cs/log-forging`); el pipeline bloquea alertas `high`/`critical` en PRs
- **PII en logs**: Los emails/correos ya no se registran en logs (AuthController, AuthService, EmailService)

## Estructura del Proyecto

```
BioGuard.Api/
├── Controllers/           # 14 controladores REST
│   ├── AuthController.cs           # Auth + JWT + 2FA + Refresh
│   ├── PacientesController.cs      # CRUD pacientes
│   ├── SensoresController.cs       # Lecturas + GPS + Eventos
│   ├── AlertasController.cs        # Alertas críticas
│   ├── MedicamentosController.cs   # CRUD medicamentos
│   ├── CuidadoresController.cs     # CRUD cuidadores
│   ├── NotificacionesController.cs # Notificaciones push
│   ├── ReportesController.cs       # Reportes + exportar CSV
│   ├── MLController.cs             # Predicciones ML
│   ├── PagosController.cs          # Pagos + recibos
│   ├── PlanesController.cs         # CRUD planes
│   ├── UsuariosWebController.cs    # Perfil de usuario
│   ├── DispositivosController.cs   # WearOS devices
│   └── AuditoriaController.cs      # Logs de auditoría
├── Models/                # 18 modelos MongoDB
│   ├── UsuarioWeb.cs       ├── Paciente.cs
│   ├── Cuidador.cs         ├── Dispositivo.cs
│   ├── LecturaSensor.cs    ├── EventoMetabolico.cs
│   ├── TrackingGps.cs      ├── Alerta.cs
│   ├── Medicamento.cs      ├── Notificacion.cs
│   ├── Pago.cs             ├── Plan.cs
│   ├── PrediccionMl.cs     ├── ModeloMl.cs
│   ├── Auditoria.cs        ├── FcmToken.cs
│   ├── RefreshToken.cs     └── TokenBlacklist.cs
├── Services/              # 15 servicios
│   ├── AuthService.cs             # JWT + PBKDF2 + 2FA + Refresh
│   ├── EmailService.cs            # MailKit SMTP
│   ├── SensorService.cs           # Lecturas + GPS + Eventos
│   ├── AlertaService.cs           # Alertas críticas
│   ├── MedicamentoService.cs      # Medicamentos + tomas
│   ├── PacienteService.cs         # Pacientes
│   ├── CuidadorService.cs         # Cuidadores
│   ├── NotificacionService.cs     # Notificaciones push
│   ├── ReporteService.cs          # Reportes
│   ├── MLService.cs               # ML + predicciones
│   ├── PagosService.cs            # Pagos
│   ├── UsuariosWebService.cs      # Perfil
│   ├── DispositivoService.cs      # WearOS
│   ├── AuditoriaService.cs        # Auditoría
│   └── BioGuardHub.cs             # SignalR hub
├── DTOs/                  # Data Transfer Objects
├── Config/                # MongoDB context + OwnershipHelper
├── Program.cs             # Pipeline: auth, CORS, rate limit, headers
├── appsettings.json       # Configuración + JWT secrets
├── Dockerfile             # Multi-stage build
└── BioGuard.Api.csproj    # .NET 9 project
```

## Base de Datos (MongoDB Atlas)

### 18 Colecciones

| Colección | Descripción |
|---|---|
| `usuarios_web` | Usuarios dueños y cuidadores |
| `pacientes` | Datos médicos de pacientes |
| `cuidadores` | Relación cuidador-paciente |
| `dispositivos` | WearOS vinculados |
| `lecturas_sensores` | Glucosa, presión, pulso, etc. (TTL) |
| `eventos_metabolicos` | Hipoglucemia, hiperglucemia, etc. |
| `tracking_gps` | Ubicación en tiempo real |
| `alertas` | Alertas críticas |
| `medicamentos` | Prescripciones médicas |
| `notificaciones` | Notificaciones push |
| `pagos` | Historial de pagos |
| `planes` | Planes de suscripción |
| `predicciones_ml` | Predicciones del modelo ML |
| `modelos_ml` | Modelos entrenados |
| `fcm_tokens` | Tokens Firebase Cloud Messaging |
| `refresh_tokens` | Refresh tokens (TTL) |
| `token_blacklist` | JWT revocados (TTL) |
| `auditoria` | Logs de actividad |

### Índices

- **lecturas_sensores**: `{ pacienteId: 1, timestamp: -1 }` + TTL en `expireAt`
- **refresh_tokens**: TTL en `expires_at`
- **token_blacklist**: TTL en `expires_at`
- **Unique indexes**: `correo` en `usuarios_web`, `macAddress` en `dispositivos`

## API Endpoints (90+)

### Auth (10 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| POST | `/api/Auth/register` | Registro con verificación email | No |
| POST | `/api/Auth/login-web` | Login web (JWT + RefreshToken) | No |
| POST | `/api/Auth/login-google` | Login Google OAuth | No |
| POST | `/api/Auth/login-codigo` | Login por código QR | No |
| POST | `/api/Auth/2FA/enviar` | Enviar código 2FA | No |
| POST | `/api/Auth/2FA/verificar` | Verificar 2FA + activar cuenta | No |
| POST | `/api/Auth/forgot-password` | Recuperar password | No |
| POST | `/api/Auth/refresh` | Renovar access token | RefreshToken |
| POST | `/api/Auth/logout` | Revocar token | JWT |
| POST | `/api/Auth/reset-password` | Cambiar password | JWT |

### Pacientes (7 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| POST | `/api/Pacientes` | Crear paciente (onboarding completo o solo nombre) | JWT (dueño) |
| GET | `/api/Pacientes/mi-paciente` | Mi paciente con datos biométricos + QR | JWT |
| GET | `/api/Pacientes/{id}` | Paciente por ID (datos completos) | JWT |
| PUT | `/api/Pacientes/{id}` | Actualizar paciente | JWT (dueño) |
| PUT | `/api/Pacientes/{id}/biometria` | Guardar/editar biometría (fecha nacimiento, sexo, peso, talla, actividad) | JWT |
| GET | `/api/Pacientes/by-usuario/{usuarioWebId}` | Listar pacientes de un usuario | JWT (dueño) |
| DELETE | `/api/Pacientes/{id}` | Eliminar paciente + cascada | JWT (dueño) |

### Sensores (15 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| POST | `/api/Sensores/lectura` | Enviar lectura | JWT (paciente_id) |
| POST | `/api/Sensores/lectura-batch` | Lote offline (max 10MB) | JWT |
| GET | `/api/Sensores/lecturas/{pacienteId}` | Historial lecturas | JWT |
| GET | `/api/Sensores/lecturas/{pacienteId}/rango` | Lecturas por rango fechas | JWT |
| GET | `/api/Sensores/lecturas/{pacienteId}/exportar-pdf` | Exportar CSV | JWT |
| GET | `/api/Sensores/estadisticas/{pacienteId}` | Estadísticas | JWT |
| GET | `/api/Sensores/estadisticas/{pacienteId}/tendencia` | Tendencia | JWT |
| POST | `/api/Sensores/evento` | Crear evento metabólico | JWT (paciente_id) |
| GET | `/api/Sensores/eventos/{pacienteId}` | Historial eventos | JWT |
| GET | `/api/Sensores/eventos/{pacienteId}/resumen` | Resumen eventos | JWT |
| PUT | `/api/Sensores/eventos/{eventoId}/atender` | Atender evento | JWT |
| POST | `/api/Sensores/tracking` | Enviar ubicación | JWT (paciente_id) |
| POST | `/api/Sensores/tracking-batch` | Lote GPS | JWT (paciente_id) |
| GET | `/api/Sensores/tracking/{pacienteId}/actual` | Ubicación actual | JWT |
| GET | `/api/Sensores/tracking/{pacienteId}/ruta` | Historial ruta | JWT |

### Alertas (6 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| POST | `/api/Alertas` | Crear alerta | JWT |
| GET | `/api/Alertas/by-paciente/{pacienteId}` | Alertas del paciente | JWT |
| GET | `/api/Alertas/pendientes/{pacienteId}` | Alertas sin resolver | JWT |
| GET | `/api/Alertas/{id}` | Alerta por ID | JWT |
| PUT | `/api/Alertas/{id}/resolver` | Resolver alerta | JWT |
| DELETE | `/api/Alertas/{id}` | Eliminar alerta | JWT |

### Medicamentos (8 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| POST | `/api/Medicamentos` | Crear medicamento | JWT (dueño) |
| POST | `/api/Medicamentos/trigger` | Trigger ML | JWT |
| GET | `/api/Medicamentos/by-paciente/{pacienteId}` | Medicamentos del paciente | JWT |
| GET | `/api/Medicamentos/{id}` | Medicamento por ID | JWT |
| PUT | `/api/Medicamentos/{id}` | Actualizar medicamento | JWT |
| PUT | `/api/Medicamentos/{id}/toma` | Registrar toma | JWT |
| PUT | `/api/Medicamentos/{id}/activo` | Activar/desactivar | JWT |
| DELETE | `/api/Medicamentos/{id}` | Eliminar medicamento | JWT |

### Cuidadores (6 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| GET | `/api/Cuidadores` | Mis cuidadores | JWT |
| GET | `/api/Cuidadores/disponibles` | Cuidadores disponibles | JWT |
| GET | `/api/Cuidadores/by-paciente/{pacienteId}` | Cuidadores del paciente | JWT |
| POST | `/api/Cuidadores` | Agregar cuidador | JWT (dueño) |
| PUT | `/api/Cuidadores/{id}` | Actualizar cuidador | JWT |
| DELETE | `/api/Cuidadores/{id}` | Eliminar cuidador | JWT |

### Reportes (5 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| GET | `/api/Reportes/resumen/{pacienteId}` | Resumen general | JWT |
| GET | `/api/Reportes/historial-alertas/{pacienteId}` | Historial alertas | JWT |
| GET | `/api/Reportes/historial-eventos/{pacienteId}` | Historial eventos | JWT |
| GET | `/api/Reportes/historial-medicamentos/{pacienteId}` | Historial medicamentos | JWT |
| GET | `/api/Reportes/historial-lecturas/{pacienteId}` | Historial lecturas | JWT |

### ML (8 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| GET | `/api/ML/predicciones/{pacienteId}` | Predicciones del paciente | JWT |
| GET | `/api/ML/predicciones/{pacienteId}/actual` | Predicción actual | JWT |
| GET | `/api/ML/recomendaciones/{pacienteId}` | Recomendaciones | JWT |
| GET | `/api/ML/modelos` | Modelos entrenados | JWT |
| GET | `/api/ML/metricas/{modeloId}` | Métricas de modelo | JWT |
| POST | `/api/ML/entrenar` | Entrenar modelo | JWT |
| POST | `/api/ML/reentrenar` | Re-entrenar modelo | JWT |
| POST | `/api/ML/diagnosticar` | Diagnóstico puntual | JWT |

### Pagos (5 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| GET | `/api/Pagos/historial` | Historial de pagos | JWT |
| POST | `/api/Pagos/crear-sesion` | Crear sesión de pago — body `{ planNombre, metodoPago: "stripe" }`; responde `checkoutUrl`, `sessionId`, `monto`, `moneda` | JWT |
| GET | `/api/Pagos/{id}/recibo` | Recibo de pago | JWT |
| POST | `/api/Pagos/cancelar` | Cancelar suscripción activa | JWT |
| POST | `/api/Pagos/webhook/stripe` | Webhook Stripe (firma verificada, `checkout.session.completed`) | Firma HMAC |

### Planes (7 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| GET | `/api/Planes` | Listar planes | No |
| GET | `/api/Planes/{id}` | Plan por ID | No |
| POST | `/api/Planes` | Crear plan | JWT (admin) |
| PUT | `/api/Planes/{id}` | Actualizar plan | JWT (admin) |
| DELETE | `/api/Planes/{id}` | Eliminar plan | JWT (admin) |
| POST | `/api/Planes/seed` | Seed planes | JWT (admin) |
| POST | `/api/Planes/migrate-prices` | Migrar precios | JWT (admin) |

### UsuariosWeb (8 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| GET | `/api/UsuariosWeb/mi-perfil` | Mi perfil | JWT |
| PUT | `/api/UsuariosWeb/mi-perfil` | Actualizar perfil | JWT |
| PUT | `/api/UsuariosWeb/mi-perfil/correo` | Cambiar correo | JWT |
| PUT | `/api/UsuariosWeb/mi-perfil/foto` | Subir foto (1MB max) | JWT |
| GET | `/api/UsuariosWeb/mi-plan` | Mi plan actual | JWT |
| PUT | `/api/UsuariosWeb/cambiar-plan` | Cambiar plan | JWT |
| GET | `/api/UsuariosWeb/by-email/{correo}` | Buscar por email | JWT |
| DELETE | `/api/UsuariosWeb/mi-cuenta` | Eliminar cuenta | JWT |

### Dispositivos (5 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| POST | `/api/Dispositivos/vincular` | Vincular WearOS | JWT (paciente_id) |
| POST | `/api/Dispositivos/heartbeat` | Keepalive | JWT (paciente_id) |
| GET | `/api/Dispositivos/{pacienteId}` | Dispositivos del paciente | JWT |
| PUT | `/api/Dispositivos/{id}` | Actualizar dispositivo | JWT |
| DELETE | `/api/Dispositivos/{id}` | Desvincular dispositivo | JWT |

### Notificaciones (6 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| GET | `/api/Notificaciones` | Mis notificaciones | JWT |
| GET | `/api/Notificaciones/by-paciente/{pacienteId}` | Notificaciones del paciente | JWT |
| GET | `/api/Notificaciones/by-usuario/{usuarioId}` | Notificaciones por usuario | JWT |
| POST | `/api/Notificaciones` | Crear notificación | JWT |
| POST | `/api/Notificaciones/fcm` | Registrar token FCM | JWT |
| PUT | `/api/Notificaciones/{id}/leer` | Marcar como leída | JWT |
| DELETE | `/api/Notificaciones/{id}` | Eliminar notificación | JWT |

### Auditoría (1 endpoint)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| GET | `/api/Auditoria` | Logs de actividad | JWT |

### Seed (2 endpoints)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| POST | `/api/Seed/seed-all` | Insertar datos de prueba | JWT (admin) |
| POST | `/api/Seed/migrate-passwords` | Migrar BCrypt -> PBKDF2 | JWT (admin) |

### Health (1 endpoint)

| Método | Ruta | Descripción | Auth |
|---|---|---|---|
| GET | `/health` | Health check | No |

## Despliegue

### URL de Producción

```
https://bioguard-api-lkvnq.ondigitalocean.app/
```

### Docker

```bash
# Build
docker build -t bioguard-api .

# Run
docker run -p 5000:8080 \
  -e MONGODB_URI="mongodb+srv://..." \
  -e MONGODB_DATABASE="bioguard" \
  -e JWT_SECRET_KEY="tu-clave-secreta" \
  -e SMTP_HOST="smtp.gmail.com" \
  -e SMTP_PORT="587" \
  -e SMTP_USER="tu@email.com" \
  -e SMTP_PASSWORD="tu-password" \
  -e SMTP_FROM="tu@email.com" \
  -e STRIPE_SECRET_KEY="sk_test_..." \
  -e STRIPE_WEBHOOK_SECRET="whsec_..." \
  bioguard-api
```

### Variables de Entorno (Requeridas)

| Variable | Descripción |
|---|---|
| `MONGODB_URI` | Connection string MongoDB Atlas |
| `MONGODB_DATABASE` | Nombre de la base de datos |
| `JWT_SECRET_KEY` | Clave secreta para JWT (min 32 chars) |
| `SMTP_HOST` | Servidor SMTP |
| `SMTP_PORT` | Puerto SMTP |
| `SMTP_USER` | Usuario SMTP |
| `SMTP_PASSWORD` | Password SMTP |
| `SMTP_FROM` | Email remitente |
| `Stripe__SecretKey` o `STRIPE_SECRET_KEY` | Clave secreta de Stripe (`sk_test_...` / `sk_live_...`) |
| `Stripe__WebhookSecret` o `STRIPE_WEBHOOK_SECRET` | Secreto del webhook de Stripe (`whsec_...`) |

### CI/CD Pipeline (GitHub Actions)

1. **Build & Test**: Compila, corre 511 tests, NuGet audit, licencias
2. **CodeQL Analysis (SAST)**: Análisis estático de seguridad (con `codeql-config.yml` que excluye el query ruidoso de "Uncontrolled Data Used in Path Expression")
3. **Secret Scanning**: Escaneo de secretos expuestos
4. **Container Security Scan**: Escaneo de vulnerabilidades en la imagen Docker
5. **Docker Build**: Build multi-stage, firmado con cosign, push a GitHub Container Registry
6. **DAST Scan (semanal)**: OWASP ZAP — análisis dinámico de seguridad contra producción
7. **Security Gate**: Bloquea el merge si el escaneo de la imagen reporta vulnerabilidades `critical` o `high` sin excepción aprobada
8. **Deploy**: DigitalOcean App Platform (auto-deploy desde master)

**Dependabot**: Habilitado con semanal, agrupando updates por ecosistema (NuGet, GitHub Actions, Docker) y 50 PRs máximo abiertos para mantener el tablero manejable.

### Branching Strategy

- `master`: rama principal (protegida, requiere PR + Build & Test + CodeQL + 1 approval)
- `rama-Liz`: rama de desarrollo activa
- PR merge a master -> deploy automático a producción

## Testing

### Ejecutar Tests

```bash
cd Test1BioGuard
dotnet test --verbosity minimal
```

### Tipos de Tests

| Tipo | Cantidad | Descripción |
|---|---|---|
| Unit Tests | ~214 | Servicios aislados con mocks |
| Integration Tests | ~110 | Endpoints HTTP completos |
| Security Tests | ~85 | IDOR, auth, input validation, timing, rate limiting |
| Non-Functional Tests | ~5 | Smoke tests (health, login, lecturas 200 OK) |

### Pruebas contra API en producción

```bash
# Script PowerShell con 46 tests end-to-end
C:\Users\perez\AppData\Local\Temp\opencode\test-bioguard.ps1
```

### Credenciales de Prueba (Seed)

```
Email:    seed_639204600292413571@bioguard.test
Password: SeedTest@123!
Paciente: 6a62d9fd3e0a61f86c97f916
Rol:      dueno
```

### Seed Endpoint

```bash
POST /api/Seed/seed-all
Authorization: Bearer <admin_token>

# Inserta todos los datos de prueba si las colecciones están vacías
# Retorna { "inserted": {...}, "skipped": [...] }
```

## Changelog

### DevSecOps — Security Gate, CodeQL config y mínima exposición de PII en logs

| Mejora | Descripción |
|---|---|
| **Security Gate** | Nuevo job `security-gate` en `security.yml`: bloquea el merge si CodeQL reporta alertas `high`/`critical` abiertas en el código del PR. |
| **CodeQL config** | Nuevo `.github/codeql/codeql-config.yml`: ignora `Test1BioGuard` y excluye la query `cs/log-forging` (falso positivo en structured logging con placeholders); el resto de `security-extended` sigue activo. |
| **PII en logs** | Eliminados correos/emails de mensajes de log en `AuthController`, `AuthService` y `EmailService` (se loggea sujeto o contexto, nunca el email completo). |

### PR #93 — Fix: crear cuidador sin usuario vinculado (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **`UsuarioVinculadoId` nullable** | `Cuidador.UsuarioVinculadoId` ahora acepta `null`, permitiendo crear un cuidador sin vincular cuenta web al momento de la creación. |
| **Tests** | Nuevos tests en `CuidadorServiceTests` para creación sin usuario vinculado y auto-vinculación. |

### PR #92 — Expirar código QR de paciente/cuidador a los 10 minutos (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **Nuevo campo `CodigoExpira`** | `DateTime?` en `Paciente` y `Cuidador` (colección: `codigo_expira`). |
| **Expiración al crear/regenerar** | Al crear o regenerar el QR (`POST`, `PUT .../regenerar-qr`, `RegenerarQRAsync`), se asigna `CodigoExpira = now + 10 min`. |
| **Validación en login** | `POST /api/Auth/login-codigo` rechaza códigos expirados (login de paciente y cuidador). |
| **Validación en vincular** | Auto-vinculación de cuidador (`self-register`) devuelve 400 `"El código ha expirado. Solicita uno nuevo al responsable."` si pasaron los 10 minutos. |
| **Respuestas** | Crear/regenerar/GET QR ahora devuelven `CodigoExpira` además de `CodigoAccesoQr`. |

### PR #71 — Devolver `checkoutUrl` al crear sesión de pago (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **Respuesta de `crear-sesion`** | `POST /api/Pagos/crear-sesion` ahora devuelve `checkoutUrl` (además de `sessionId`, `monto`, `moneda`). El frontend debe redirigir a esa URL. |
| **Motivación** | La URL de checkout de Stripe lleva un fragmento cifrado (`#fidnand...`) que **no se puede reconstruir** a partir del `sessionId`; si el frontend intenta armarla, el pago falla. |
| **Persistencia** | Nuevo campo `checkout_url` en la colección `pagos` para referencia. |
| **Tests** | 483/483 correctos, build 0 warnings. Deployado a producción. |

### PR #70 — Fix: aceptar cualquier `api_version` en webhook de Stripe (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **Bug en webhook** | Stripe firma los eventos con su `api_version` actual; si no coincide con la versión de la librería (Stripe.net), `ConstructEvent` lanzaba excepción y el webhook respondía 500. |
| **Solución** | `EventUtility.ConstructEvent(payload, signature, secret, 300, throwOnApiVersionMismatch: false)` en `VerifyWebhookSignatureAsync` y `ParseWebhookEventAsync` (`StripePaymentGateway.cs`). |
| **Verificado en producción** | Webhook responde `200 {"received":true}` con firma válida real. |
| **Tests nuevos** | `StripePaymentGatewayTests` (4 tests): firma válida, firma inválida, api_version distinta, parseo de evento. |

### PR #69 — Eliminar Mercado Pago: pagos solo con Stripe (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **Solo Stripe** | Eliminados `MercadoPagoPaymentGateway.cs`, `MercadoPagoOptions.cs`, `MercadoPagoPreferenceId` (modelo), webhook `POST /api/Pagos/webhook/mercadopago` y la sección `MercadoPago` de `appsettings.json`. |
| **Validación** | `metodoPago` solo acepta `"stripe"`; cualquier otro valor responde 400. |
| **Precios reales (MXN)** | Familiar **$1** (`price_1TzX4KLjTPFQFc1GZC6A6wtC`, product `prod_UzW6FlFfBVSYPG`) y Pro **$2** (`price_1TzX4KLjTPFQFc1GwETqGdKG`, product `prod_UzW6PJYpeXDG2p`), suscripciones mensuales, modo test. |
| **MongoDB** | `planes.stripe_price_id` actualizado para Familiar y Pro. |
| **Webhook Stripe** | Endpoint creado hacia producción (evento `checkout.session.completed`). |
| **Config robusta** | `Program.cs` acepta `Stripe__SecretKey`/`Stripe__WebhookSecret` o `STRIPE_SECRET_KEY`/`STRIPE_WEBHOOK_SECRET`. Variables configuradas en DigitalOcean. |
| **Fallback de precio** | Si `plan.StripePriceId` está vacío, el gateway crea el precio automáticamente (`precio*100` MXN mensual). |

### PR #67 — Datos Biométricos Completos del Paciente (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **Onboarding móvil completo** | `POST /api/Pacientes` ahora acepta y guarda fecha de nacimiento, peso (kg), estatura (cm), sexo, actividad física, diabetes (paciente y familiares) en un solo request. Retrocompatible: sigue funcionando solo con `{ nombre }`. |
| **Nuevo campo `sexo`** | Agregado a `Biometria` en el modelo y colección `pacientes`. |
| **PUT biometria ampliado** | `/api/Pacientes/{id}/biometria` ahora también guarda fecha de nacimiento y sexo. |
| **GET con datos completos** | `mi-paciente`, `/{id}` y `/by-usuario/{id}` devuelven todos los datos biométricos + `codigoAccesoQr` (el perfil móvil ya puede mostrarlos). |
| **Validación de seguridad** | Fecha de nacimiento futura rechazada (400) en POST y PUT biometría; strings con `[StringLength]`; ownership check (IDOR) intacto en todos los endpoints. |
| **Fix en tests** | Tests de biometría dependían de mocks compartidos (contaminación); ahora son independientes y pasan en aislamiento. |
| **Tests** | **480 tests**, 0 fallos, 0 warnings. |

### PR #66 — Endurecer Rate Limiting + Smoke Tests (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **Reglas de rate limit por endpoint** | `POST /api/Auth/refresh` 10/min, `POST /api/Auth/reset-password` 3/min, `PUT /api/Auth/cambiar-password` 3/min, `POST /api/Sensores/lectura` 60/min, `POST /api/Sensores/lectura-batch` 10/min. |
| **Smoke tests** | Nuevo proyecto `NonFunctionalTests`: `/health` 200 healthy, `login-web` 200 con token, `Sensores/lectura` 200. |
| **Fix warning CS1998** | `MercadoPagoPaymentGateway.VerifyWebhookSignatureAsync` convertido a síncrono (0 warnings). |
| **Tests** | 473 tests, 0 fallos, 0 warnings. |

### PR #65 — Pagos Reales Stripe + Mercado Pago con Webhooks Seguros y Precios MXN (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **Gateways reales** | `StripePaymentGateway` y `MercadoPagoPaymentGateway` implementando `IPaymentGateway`, elegibles con `metodoPago` en `crear-sesion`. |
| **Checkout real** | Creación de sesión/preferencia en Stripe y Mercado Pago, redirección al gateway, confirmación vía webhook. |
| **Webhooks seguros** | `POST /api/Pagos/webhook/stripe` (firma HMAC de Stripe) y `POST /api/Pagos/webhook/mercadopago` (firma `ts` + `v1` verificada con `FixedTimeEquals`). Sin credenciales expuestas. |
| **Precios MXN** | Planes y montos en pesos mexicanos (MXN). |
| **PagosService** | Actualizado para persistir sesiones y confirmar pagos de ambos gateways. |
| **Tests** | Tests de integración para ambos gateways y webhooks. |

### PR #63 — Correcciones de Seguridad + Cobertura Total de Tests (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **Data leak tracking GPS** | Filtro `Eq` roto en `SensorService.ObtenerTrackingAsync` retornaba datos de **todos los pacientes**. Corregido para filtrar por `PacienteId`. |
| **NullReferenceException en Biometría** | `UpdateBiometriaAsync` fallaba si `Biometria == null`. Se reemplaza todo el objeto en lugar de campos anidados. |
| **Error masking** | `AuditoriaController` retornaba `200 OK` en excepción. Ahora retorna `500`. |
| **FCM null role** | `NotificacionesController.RegistrarFcm` validaba `usuarioId` pero no `role`. Ahora rechaza si falta. |
| **IPaymentGateway duplicado** | Stripe y PayPal registrados como `IPaymentGateway` — solo PayPal era resuelto. Se eliminan (código muerto). |
| **IImageStorageService redundante** | `AddScoped` duplicado antes de `AddHttpClient`. Eliminado el redundante. |
| **CS8618 eliminado** | `_database = null!` en constructor `protected` de `MongoDbContext`. |
| **Cobertura de tests al 100%** | **44 tests nuevos** para AdminController, MLController, login-google, refresh, logout, vincular, FCM y migrate-prices. **458 tests**, 0 fallos. |
| **0 warnings, 0 errores** | Build completamente limpio. |

### PR #62 — Logout Total + Multi-Rol Refresh Token (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **Logout total** | `POST /api/Auth/logout-all` — revoca todas las sesiones del usuario y blacklistea el token actual. |
| **Multi-rol refresh token** | `RefreshTokenAsync` ahora maneja correctamente roles `dueno`, `paciente` y `cuidador` al regenerar tokens. |
| **Role preservado** | El rol se almacena en el refresh token y se restaura al refrescar. |
| **Tests** | Tests de integración para `logout-all` (éxito y sin token). |

### PR #61 — Integración Zip de Liz + Máxima Seguridad (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **AdminController** | Endpoints de administración: listar/buscar usuarios, ver detalle, pausar/reactivar cuentas, tickets soporte, métricas del sistema. |
| **Modelos nuevos** | `TicketSoporte`, `ReporteCompartido`, `DeviceSession` con sus colecciones en MongoDB. |
| **MLService + MLController** | Módulo 6 (AI Console): predicciones, recomendaciones, diagnóstico, entrenamiento/reentrenamiento de modelos, métricas. |
| **Pagos con métodos** | `PagosService` con historial, cancelación. Gateways Stripe/PayPal preparados. |
| **Notificaciones push** | `FirebasePushNotificationService` + `FcmToken` para push notifications. |
| **PlanLimiteService** | Validación de límites por plan (pacientes, cuidadores, GPS, AI Console). |
| **Reportes compartidos** | Endpoints para compartir reportes clínicos. |
| **Seguridad máxima** | Ownership checks, rate limiting, validación de entrada, headers de seguridad. |
| **Tests** | Cobertura completa de servicios y controladores. |

### PR #60 — Auditoría de Seguridad Integral (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **IDOR en MLController** | Ownership checks en `ObtenerPredicciones`, `PrediccionActual`, `Recomendaciones`, `Diagnosticar` |
| **IDOR en NotificacionesController** | Ownership checks en `Crear`, `MarcarLeida`, `Eliminar` + fix en `ObtenerPorUsuario` |
| **IDOR en PagosController** | Ownership check en `Recibo` (verifica `Pago.UsuarioWebId == currentUser`) |
| **Input validation** | `[StringLength]`, `[Range]` en DTOs de ML, Notificaciones, Pagos, Planes |
| **Claim estandarizado** | `ClaimTypes.NameIdentifier` en `UsuariosWebController` y `PagosController` (faltaban) |
| **MAC masking** | Direcciones MAC mostradas como `XX:XX:XX:XX:XX:XX` en respuestas |
| **Sensitive data logging** | Eliminados códigos de acceso, passwords y valores QR de logs |
| **423 tests pasando** | 100% verdes |
| **46/46 API tests** | Todos OK contra DigitalOcean |

### PR #59 — Migración de Passwords (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **Endpoint `/api/Seed/migrate-passwords`** | Busca hashes BCrypt en la DB y los reemplaza con PBKDF2 (600K iteraciones) |
| **Migración ejecutada** | 2 usuarios convertidos (`juan.perez@email.com`, `test@test.com`) |

### PR #58 — Fix SHA Pins en DAST (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **zaproxy/action-baseline** | SHA fijado a `6c5a007541891231cd9e0ddec25d4f25c59c9874` (v0.15.0) |
| **zaproxy/action-api-scan** | SHA fijado a `bd24b11e76da11ab60302c8ae87f091cbb11f034` (v0.10.0) |
| **DAST workflow** | Ahora corre exitosamente (semanal + PRs a master) |

### PR #57 — 4 Bugs Críticos en Sincronización Web-Móvil (rama-Liz -> master)

| Fix | Descripción |
|---|---|
| **QR regeneration** | Si el usuario ya tiene un código QR, se retorna el existente (no se duplica) |
| **FCM push endpoint** | `POST /api/Notificaciones/fcm` ahora registra/actualiza tokens push correctamente |
| **Cuidador self-vinculación** | Endpoint de vinculación ahora es `[AllowAnonymous]` (el cuidador no tiene JWT aún) |
| **Token `paciente_id`** | Ahora se incluye en `GenerateToken()` para dueños (ya estaba para pacientes) |
| **423 tests pasando** | 100% verdes |
| **45/45 API tests** | Todos OK contra DigitalOcean |

### PR #56 — Fix Refresh Token y Auditoría (rama-Liz)

| Fix | Descripción |
|---|---|
| **Refresh Token 500** | `CryptographicOperations.FixedTimeEquals` no puede ser traducido por MongoDB LINQ provider. Reemplazado por comparación de strings en queries de BD |
| **Auditoría 500** | Documentos legacy con `entidad_id` como ObjectId fallan al deserializar. Wrapper try-catch que retorna lista vacía |

### PR #55 — Fixes generales

| Fix | Descripción |
|---|---|
| **Refresh Token en login** | Los 4 métodos de login ahora crean RefreshToken en DB |
| **Alerta accionTomada** | Campo `accion_tomada` guardado correctamente en MongoDB |
| **ExportarPDF** | Retorna CSV descargable con datos reales |
| **Pagos Recibo** | Retorna datos reales del pago |
| **Cascade delete** | Eliminar paciente también elimina cuidadores asociados |
| **OwnershipHelper** | Lógica de verificación extraída a clase compartida (era copy-paste en 7 controllers) |

### PR #54 — Email y Seguridad

| Fix | Descripción |
|---|---|
| **Email verification** | Registro crea usuario inactivo, código 6-dígitos, verificación activa cuenta |
| **Forgot password** | Ahora envía email real vía MailKit |
| **MailKit upgrade** | 4.11.0 -> 4.17.0 (vulnerabilidad GHSA-9j88-vvj5-vhgr) |

### Seguridad Acumulada (todos los PRs)

- IDOR protection via `OwnershipHelper` en 14 controladores
- Cuidador ownership verification en todos los endpoints
- Role-based auth `[Authorize(Roles = "dueno")]` en endpoints de escritura
- Timing-safe 2FA comparison
- Account lockout (5 intentos / 15 min)
- Password complexity validation (8+ chars, mayúscula, minúscula, dígito, especial)
- CSP + security headers
- Rate limiting per-endpoint
- Request size limits (1MB foto, 10MB batch)
- Token blacklist + refresh token rotation
- 2FA enforcement en `UsuarioWeb`
- MAC address masking (`XX:XX:XX:XX:XX:XX`)
- Sensitive data eliminado de logs
- DAST scanning con OWASP ZAP
- Migración BCrypt -> PBKDF2 (600K iteraciones)
- `ClaimTypes.NameIdentifier` estandarizado en todos los controladores
