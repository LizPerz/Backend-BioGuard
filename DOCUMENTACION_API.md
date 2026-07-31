# BioGuard API — Documentación Maestra del Backend

Esta es la documentación técnica y de seguridad detallada para todos los endpoints expuestos por la API REST de **BioGuard**.

---

## 1. MÓDULO DE AUTENTICACIÓN (`api/Auth`)

### 1.1 `POST /api/Auth/register`
* **Propósito:** Registra un nuevo usuario web principal (dueño), asignándole el plan gratuito inicial y encriptando su contraseña.
* **Autenticación:** Ninguna (Público).
* **Roles con acceso (RBAC):** Público. Rol resultante: `dueno`.
* **Parámetros:** Body JSON: `RegisterWebRequest`
  * `nombre` (string, requerido, max 100)
  * `apellidoPaterno` (string, requerido, max 100)
  * `correo` (string, requerido, formato email)
  * `password` (string, requerido, min 8, max 128)
  * `planNombre` (string, requerido, max 100)
  * `apellidoMaterno` (string, opcional, max 100)
* **Validaciones:**
  * El correo no debe estar registrado previamente.
  * Complejidad de contraseña (mínimo 8 caracteres, mayúsculas, minúsculas, números y símbolos).
  * El nombre del plan debe coincidir con un plan existente en BD.
* **Colecciones / recursos de BD:** `usuarios_web` (insert), `planes` (lookup).
* **Usado en:** Pantalla de Registro de la Web y App Móvil.
* **Dependencias con otros servicios:** Ninguna.
* **Medidas de seguridad:**
  * Hasheo de contraseña utilizando `BCrypt` / `PBKDF2` robusto.
  * Sanitización de cadenas (`nombre`, `apellidos`) para prevenir inyecciones HTML/XSS.
* **Códigos de respuesta:** 200 OK, 400 Bad Request (correo duplicado, contraseña débil o plan inválido).
* **Ejemplo de entrada (JSON):**
  ```json
  {
    "nombre": "Carlos",
    "apellidoPaterno": "Perez",
    "correo": "carlos@example.com",
    "password": "Password123!",
    "planNombre": "Gratis"
  }
  ```
* **Ejemplo de salida (JSON):**
  ```json
  {
    "message": "Usuario registrado exitosamente"
  }
  ```

---

### 1.2 `POST /api/Auth/login-web`
* **Propósito:** Autentica a un usuario web (dueño) mediante credenciales tradicionales.
* **Autenticación:** Ninguna.
* **Roles con acceso (RBAC):** Público.
* **Parámetros:** Body JSON: `LoginWebRequest`
  * `correo` (string, requerido)
  * `password` (string, requerido)
* **Validaciones:**
  * El correo debe estar registrado y activo.
  * Comprobación del password hash.
  * Control de bloqueo por intentos fallidos.
* **Colecciones / recursos de BD:** `usuarios_web` (lookup y update de intentos fallidos).
* **Usado en:** Pantalla de Login de la Web y App Móvil (Dueños).
* **Dependencias con otros servicios:** Ninguna.
* **Medidas de seguridad:**
  * Bloqueo de cuenta temporal tras 5 intentos fallidos consecutivos para mitigar ataques de fuerza bruta.
  * Generación de tokens JWT firmados digitalmente (HMAC SHA-256).
* **Códigos de respuesta:** 200 OK, 400 Bad Request (credenciales incorrectas), 403 Forbidden (cuenta bloqueada temporalmente).
* **Ejemplo de entrada (JSON):**
  ```json
  {
    "correo": "carlos@example.com",
    "password": "Password123!"
  }
  ```
* **Ejemplo de salida (JSON):**
  ```json
  {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "userId": "66f1a2b3c4d5e6f7a8b9c0d1",
    "nombre": "Carlos",
    "rol": "dueno",
    "plan": "Gratis",
    "requires2FA": false
  }
  ```

---

### 1.3 `POST /api/Auth/login-google`
* **Propósito:** Autentica o registra automáticamente a un dueño mediante inicio de sesión con Google.
* **Autenticación:** Ninguna (validación OAuth2).
* **Roles con acceso (RBAC):** Público.
* **Parámetros:** Body JSON: `LoginGoogleRequest`
  * `idToken` (string, requerido)
* **Validaciones:**
  * El token de Google debe ser verificado con el servidor oficial.
  * El campo `email_verified` debe ser true.
* **Colecciones / recursos de BD:** `usuarios_web` (lookup / insert), `planes` (lookup del plan inicial).
* **Usado en:** Botón "Continuar con Google" de la app móvil y web.
* **Dependencias con otros servicios:** Google OAuth2 API (`https://oauth2.googleapis.com/tokeninfo`).
* **Medidas de seguridad:** Verificación del emisor (`iss`), audiencia (`aud`) e integridad de firma del token provisto por Google.
* **Códigos de respuesta:** 200 OK, 401 Unauthorized (token inválido o expirado).
* **Ejemplo de entrada (JSON):**
  ```json
  {
    "idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6..."
  }
  ```
* **Ejemplo de salida (JSON):**
  ```json
  {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "userId": "66f1a2b3c4d5e6f7a8b9c0d1",
    "nombre": "Carlos Google",
    "rol": "dueno",
    "plan": "Gratis"
  }
  ```

---

### 1.4 `POST /api/Auth/login-codigo`
* **Propósito:** Permite el inicio de sesión de relojes (pacientes) o cuidadores en dispositivos móviles utilizando el código generado en el QR.
* **Autenticación:** Ninguna.
* **Roles con acceso (RBAC):** Público. Rol resultante: `paciente` o `cuidador`.
* **Parámetros:** Body JSON: `LoginCodigoRequest`
  * `codigoAcceso` (string, requerido)
* **Validaciones:**
  * El código QR/alfanumérico debe estar registrado en BD, activo y sin expirar.
* **Colecciones / recursos de BD:** `pacientes` (lookup), `cuidadores` (lookup).
* **Usado en:** Escaneo QR o vinculación alfanumérica de la app móvil del cuidador y del reloj WearOS.
* **Dependencias con otros servicios:** Ninguna.
* **Medidas de seguridad:**
  * Bloqueo tras múltiples intentos fallidos para prevenir escaneos de fuerza bruta de códigos alfanuméricos de 8 caracteres.
* **Códigos de respuesta:** 200 OK, 400 Bad Request (código expirado, bloqueado o inválido).
* **Ejemplo de entrada (JSON):**
  ```json
  {
    "codigoAcceso": "ABC12345"
  }
  ```
* **Ejemplo de salida (JSON):**
  ```json
  {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "d7a8b9c0d1e2...",
    "userId": "66f1a2b3c4d5e6f7a8b9c0e2",
    "nombre": "Paciente Juan",
    "rol": "paciente"
  }
  ```

---

### 1.5 `POST /api/Auth/forgot-password`
* **Propósito:** Inicia el flujo de recuperación de contraseña enviando un token de 1 hora de duración al correo del usuario.
* **Autenticación:** Ninguna.
* **Roles con acceso (RBAC):** Público.
* **Parámetros:** Body JSON: `ForgotPasswordRequest`
  * `correo` (string, requerido, formato email)
* **Validaciones:**
  * Valida que el formato del correo sea correcto.
* **Colecciones / recursos de BD:** `usuarios_web` (lookup y update de token/expiración).
* **Usado en:** Botón "¿Olvidaste tu contraseña?" en el formulario de acceso web/móvil.
* **Dependencias con otros servicios:** Servicio de correo (`EmailService`).
* **Medidas de seguridad:**
  * Generación de un token criptográfico seguro de alta entropía.
  * Expiración estricta de 1 hora.
  * Respuesta genérica para evitar enumeración de cuentas.
* **Códigos de respuesta:** 200 OK.
* **Ejemplo de entrada (JSON):**
  ```json
  {
    "correo": "carlos@example.com"
  }
  ```
* **Ejemplo de salida (JSON):**
  ```json
  {
    "message": "Si el correo está registrado, recibirás un link de recuperación"
  }
  ```

---

### 1.6 `POST /api/Auth/reset-password`
* **Propósito:** Restablece la contraseña de un usuario mediante el token de recuperación recibido.
* **Autenticación:** Ninguna.
* **Roles con acceso (RBAC):** Público.
* **Parámetros:** Body JSON: `ResetPasswordRequest`
  * `token` (string, requerido)
  * `correo` (string, requerido)
  * `nuevaPassword` (string, requerido, min 8)
* **Validaciones:**
  * El token debe coincidir con el almacenado y no haber expirado.
  * Validación de complejidad de la nueva contraseña.
* **Colecciones / recursos de BD:** `usuarios_web` (lookup y actualización de contraseña, remoción de tokens de recuperación).
* **Usado en:** Formulario del enlace de restablecimiento enviado por correo.
* **Dependencias con otros servicios:** Servicio de correo (notifica cambio de contraseña).
* **Medidas de seguridad:**
  * Revocación automática de toda la cadena de Refresh Tokens tras cambiar contraseña para cerrar sesiones anteriores.
* **Códigos de respuesta:** 200 OK, 400 Bad Request (token inválido, expirado o contraseña débil).
* **Ejemplo de entrada (JSON):**
  ```json
  {
    "token": "token_recuperacion_123",
    "correo": "carlos@example.com",
    "nuevaPassword": "NewPassword123!"
  }
  ```
* **Ejemplo de salida (JSON):**
  ```json
  {
    "message": "Contraseña actualizada correctamente"
  }
  ```

---

## 2. MÓDULO DE PACIENTES (`api/Pacientes`)

### 2.1 `GET /api/Pacientes/{id}/dashboard-summary`
* **Propósito:** Devuelve un resumen consolidado de toda la información del paciente para el renderizado del Dashboard.
* **Autenticación:** Bearer JWT.
* **Roles con acceso (RBAC):** `dueno`, `cuidador` (con permisos de historial completo).
* **Parámetros:** Path param: `id` (string, requerido, ObjectId del paciente).
* **Validaciones:**
  * Verificación de propiedad (el paciente debe pertenecer al usuario autenticado).
* **Colecciones / recursos de BD:** `pacientes`, `lecturas_sensores`, `tracking_gps`, `dispositivos`, `alertas`, `eventos_metabolicos`.
* **Usado en:** Dashboard principal de la web y app móvil al seleccionar un paciente.
* **Dependencias con otros servicios:** `SensorService` y `DispositivoService`.
* **Medidas de seguridad:**
  * Descifrado de coordenadas GPS al vuelo mediante AES-256.
  * Validation estricta de propiedad (previene vulnerabilidades IDOR).
* **Códigos de respuesta:** 200 OK, 401 Unauthorized, 403 Forbidden, 404 Not Found.
* **Ejemplo de entrada (JSON):** Ninguno (Petición GET).
* **Ejemplo de salida (JSON):**
  ```json
  {
    "paciente": {
      "id": "66f1a2b3c4d5e6f7a8b9c0d2",
      "nombre": "Juan Perez",
      "esDiabetico": true,
      "perfilCompletado": true
    },
    "ultimaLectura": {
      "timestamp": "2026-07-30T22:00:00Z",
      "pulsoBpm": 80,
      "temperaturaC": 36.5,
      "sudoracionGsr": 1.8,
      "hrv": 55,
      "spo2": 98,
      "probabilidadPico": 0.05,
      "nivelRiesgo": "Bajo"
    },
    "ultimaUbicacion": {
      "longitud": -99.1332,
      "latitud": 19.4326,
      "timestamp": "2026-07-30T22:01:00Z",
      "esEmergencia": false
    },
    "dispositivo": {
      "vinculado": true,
      "nombreDispositivo": "Galaxy Watch 6",
      "macAddress": "AA:BB:CC:DD:EE:FF",
      "conectado": true
    },
    "alertasPendientesCount": 0,
    "alertasRecientes": [],
    "eventosRecientes": []
  }
  ```

---

### 2.2 `PUT /api/Pacientes/{id}/biometria`
* **Propósito:** Registra y calcula los parámetros biométricos (onboarding del paciente).
* **Autenticación:** Bearer JWT.
* **Roles con acceso (RBAC):** `dueno`, `paciente` (móvil durante el onboarding).
* **Parámetros:** 
  * Path param: `id` (ObjectId del paciente)
  * Body JSON: `UpdateBiometriaRequest`
    * `fechaNacimiento` (DateTime, requerido)
    * `sexo` (string, requerido, "M"|"F"|"Otro")
    * `pesoKg` (double, requerido, 0.1 - 500)
    * `estaturaCm` (double, requerido, 20 - 300)
    * `esDiabetico` (bool, requerido)
    * `familiaresDiabetes` (bool, requerido)
    * `actividadFisica` (string, requerido, max 50)
* **Validaciones:**
  * Rangos de validación de entradas.
  * Verificación de propiedad del paciente.
* **Colecciones / recursos de BD:** `pacientes` (update).
* **Usado en:** Paso 1 del onboarding de la app móvil del paciente.
* **Dependencias con otros servicios:** Ninguna.
* **Medidas de seguridad:**
  * Cálculo dinámico de edad en backend para evitar manipulación de datos del cliente.
  * Sanitización del texto de actividad física.
* **Códigos de respuesta:** 200 OK, 400 Bad Request, 403 Forbidden.
* **Ejemplo de entrada (JSON):**
  ```json
  {
    "fechaNacimiento": "1996-07-30T00:00:00Z",
    "sexo": "M",
    "pesoKg": 75.5,
    "estaturaCm": 175.0,
    "esDiabetico": false,
    "familiaresDiabetes": false,
    "actividadFisica": "Moderada"
  }
  ```
* **Ejemplo de salida (JSON):**
  ```json
  {
    "message": "Biometría actualizada"
  }
  ```

---

## 3. MÓDULO DE DISPOSITIVOS (`api/Dispositivos`)

### 3.1 `GET /api/Dispositivos/{pacienteId}/info-completa`
* **Propósito:** Obtiene el estado técnico consolidado de conectividad y batería tanto del smartwatch como del teléfono del paciente.
* **Autenticación:** Bearer JWT.
* **Roles con acceso (RBAC):** `dueno`, `cuidador` (con nivel de acceso completo).
* **Parámetros:** Path param: `pacienteId` (string, requerido).
* **Validaciones:** Verificación de pertenencia del paciente al usuario.
* **Colecciones / recursos de BD:** `dispositivos` (lookup), `device_sessions` (lookup).
* **Usado en:** Sección "Ficha Técnica / Estado del Dispositivo" en la web y móvil.
* **Dependencias con otros servicios:** `DispositivoService`.
* **Medidas de seguridad:** Mitigación de IDOR.
* **Códigos de respuesta:** 200 OK, 403 Forbidden.
* **Ejemplo de salida (JSON):**
  ```json
  {
    "reloj": {
      "modelo": "Samsung Watch 5",
      "conectado": true,
      "bateria": 88,
      "ultimaSincronizacion": "2026-07-30T22:30:00Z",
      "sensoresDisponibles": ["pulso", "gsr", "spo2"]
    },
    "telefono": {
      "modelo": "Google Pixel 7",
      "sistemaOperativo": "Android 14",
      "bateria": 75,
      "ahorroEnergia": false,
      "conectividad": "wifi"
    }
  }
  ```

---

## 4. MÓDULO DE PAGOS Y FACTURACIÓN (`api/Pagos`)

### 4.1 `GET /api/Pagos/{id}/recibo/descarga`
* **Propósito:** Genera y descarga el recibo oficial de pago de suscripción en texto plano (.txt).
* **Autenticación:** Bearer JWT.
* **Roles con acceso (RBAC):** `dueno` propietario del pago.
* **Parámetros:** Path param: `id` (string, requerido, ObjectId del pago).
* **Validaciones:** Comprobación estricta de propiedad (el pago debe haber sido emitido por el usuario logueado).
* **Colecciones / recursos de BD:** `pagos` (lookup), `planes` (lookup).
* **Usado en:** Historial de facturación, botón "Descargar Recibo".
* **Dependencias con otros servicios:** Ninguna.
* **Medidas de seguridad:**
  * Mitigación de IDOR en archivos de descarga.
  * Trazabilidad mediante auditoría de descargas.
* **Códigos de respuesta:** 200 OK (Archivo adjunto), 403 Forbidden, 404 Not Found.
* **Ejemplo de salida (Archivo descargado `recibo_pago1.txt`):**
  ```text
  ==================================================
                 RECIBO DE PAGO BIOGUARD            
  ==================================================
  ID de Transacción: pago1
  Fecha de Transacción: 30/07/2026 22:15:00 UTC
  Método de Pago: STRIPE
  Estado: COMPLETADO
  --------------------------------------------------
  Concepto: Suscripción - Familiar
  Monto: 129 MXN
  ==================================================
         Gracias por confiar en BioGuard.           
  ==================================================
  ```

---

## 5. MÓDULO DE SENSORES Y TELEMETRÍA (`api/Sensores`)

### 5.1 `POST /api/Sensores/lecturas`
* **Propósito:** Ingiere lecturas de signos vitales recolectadas por el reloj inteligente y calcula el nivel de riesgo metabólico en tiempo real.
* **Autenticación:** Bearer JWT.
* **Roles con acceso (RBAC):** `paciente` (el WearOS autenticado).
* **Parámetros:** Body JSON: Colección/Arreglo de lecturas fisiológicas.
* **Validaciones:**
  * Formato de datos médicos correctos.
  * Validación del token que corresponda al paciente que envía los datos.
* **Colecciones / recursos de BD:** `lecturas_sensores` (insert), `alertas` (insert si es crítico), `eventos_metabolicos` (insert).
* **Usado en:** Background service de sincronización del WearOS.
* **Dependencias con otros servicios:** `RiesgoMetabolicoService`, `AlertaService`, `FCMService` (Firebase Push).
* **Medidas de seguridad:**
  * Rate-limiting estricto a nivel de aplicación para evitar ataques de denegación de servicio por simulación de dispositivos.
* **Códigos de respuesta:** 200 OK.
* **Ejemplo de entrada (JSON):**
  ```json
  [
    {
      "pulsoBpm": 85,
      "temperaturaC": 36.6,
      "sudoracionGsr": 2.1,
      "hrv": 60,
      "spo2": 97,
      "timestamp": "2026-07-30T22:35:00Z"
    }
  ]
  ```
* **Ejemplo de salida (JSON):**
  ```json
  {
    "message": "Lecturas procesadas"
  }
  ```

---

### 5.2 `POST /api/Sensores/tracking`
* **Propósito:** Recibe e inserta las coordenadas GPS actuales enviadas por el teléfono móvil del paciente.
* **Autenticación:** Bearer JWT.
* **Roles con acceso (RBAC):** `paciente` (móvil).
* **Parámetros:** Body JSON:
  * `latitud` (double, requerido, -90 a 90)
  * `longitud` (double, requerido, -180 a 180)
  * `esEmergencia` (bool, requerido)
* **Validaciones:** Rangos válidos de coordenadas terrestres.
* **Colecciones / recursos de BD:** `tracking_gps` (insert).
* **Usado en:** Servicio de localización en segundo plano en la app móvil.
* **Dependencias con otros servicios:** `CriptoService` (Cifrado AES-256).
* **Medidas de seguridad:**
  * Cifrado simétrico AES-256 de las coordenadas en la propiedad `UbicacionCifrada` antes de escribirse en disco.
* **Códigos de respuesta:** 200 OK.
* **Ejemplo de entrada (JSON):**
  ```json
  {
    "latitud": 19.4326,
    "longitud": -99.1332,
    "esEmergencia": false
  }
  ```
* **Ejemplo de salida (JSON):**
  ```json
  {
    "message": "Ubicación registrada"
  }
  ```

---

### 5.3 `GET /api/Sensores/tracking/{pacienteId}/actual`
* **Propósito:** Obtiene la última ubicación geográfica registrada del paciente, descifrada.
* **Autenticación:** Bearer JWT.
* **Roles con acceso (RBAC):** `dueno`, `cuidador` (con privilegios de historial).
* **Parámetros:** Path param: `pacienteId`.
* **Validaciones:** Mitigación de IDOR.
* **Colecciones / recursos de BD:** `tracking_gps` (lookup).
* **Usado en:** Panel del Mapa del Paciente en la web y móvil.
* **Dependencias con otros servicios:** `CriptoService`.
* **Medidas de seguridad:**
  * Descifrado de la base de datos a nivel de memoria RAM antes de enviar al cliente.
* **Códigos de respuesta:** 200 OK, 403 Forbidden.
* **Ejemplo de salida (JSON):**
  ```json
  {
    "latitud": 19.4326,
    "longitud": -99.1332,
    "timestamp": "2026-07-30T22:36:00Z",
    "esEmergencia": false
  }
  ```
