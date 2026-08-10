using System.ComponentModel.DataAnnotations;

namespace BioGuard.Api.DTOs;

// ── Auth ──────────────────────────────────────────────────

public record RegisterWebRequest(
    [Required] [StringLength(100)] string Nombre,
    [Required] [StringLength(100)] string ApellidoPaterno,
    [Required] [EmailAddress] string Correo,
    [Required] [MinLength(8)] [StringLength(128)] string Password,
    [Required] string PlanNombre,
    [StringLength(100)] string ApellidoMaterno = "");

public record LoginWebRequest(
    [Required] [EmailAddress] string Correo,
    [Required] string Password);

public record LoginGoogleRequest(
    [Required] string IdToken);

public record LoginCodigoRequest(
    [Required] [StringLength(50)] string CodigoAcceso);

public record AuthResponse(string Token, string UserId, string Nombre, string Rol, string Plan, bool Requires2FA = false, bool RequiresVerification = false, string? RefreshToken = null);

public record RegisterResult(AuthResponse? Response, string? Error);

public record LoginResult(AuthResponse? Response, string? Error);

public record RefreshTokenRequest(
    [Required] string RefreshToken);

public record RefreshTokenResponse(string AccessToken, string RefreshToken);

public record Enviar2FARequest(
    [Required] [EmailAddress] string Correo);

public record Verificar2FARequest(
    [Required] [EmailAddress] string Correo,
    [Required] [StringLength(6, MinimumLength = 6)] string Codigo);

public record ForgotPasswordRequest(
    [Required] [EmailAddress] string Correo);

public record ResetPasswordRequest(
    [Required] string Token,
    [Required] [MinLength(8)] [StringLength(128)] string NuevaPassword);

public record MarcarResetAbiertoRequest(
    [Required] string RequestId);

public record ForgotPasswordResult(bool Success, string? RequestId, string? Token);

public record CambiarPasswordRequest(
    [Required] string PasswordActual,
    [Required] [MinLength(8)] [StringLength(128)] string NuevaPassword);

// ── Pacientes ─────────────────────────────────────────────

public record PacienteResponse(
    string Id, string Nombre, bool EsDiabetico,
    bool PerfilCompletado,
    DateTime? FechaNacimiento = null,
    int Edad = 0,
    double PesoKg = 0,
    double EstaturaCm = 0,
    string? Sexo = null,
    bool FamiliaresDiabetes = false,
    string? ActividadFisica = null,
    string? CodigoAccesoQr = null);

public record UpdateBiometriaRequest(
    [Range(0, 150)] int Edad,
    [Range(0.1, 500)] double PesoKg,
    [Range(20, 300)] double EstaturaCm,
    bool EsDiabetico, bool FamiliaresDiabetes,
    [StringLength(50)] string ActividadFisica,
    DateTime? FechaNacimiento = null,
    [StringLength(20)] string? Sexo = null);

public record CrearPacienteRequest(
    [Required] [StringLength(200)] string Nombre,
    DateTime? FechaNacimiento = null,
    int Edad = 0,
    double PesoKg = 0,
    double EstaturaCm = 0,
    [StringLength(20)] string? Sexo = null,
    bool EsDiabetico = false,
    bool FamiliaresDiabetes = false,
    [StringLength(50)] string? ActividadFisica = null);
public record UpdateNombreRequest(
    [Required] [StringLength(200)] string Nombre);

// ── Cuidadores ────────────────────────────────────────────

public record CuidadorResponse(
    string Id, string Nombre, string Parentesco,
    string PacienteId, string Telefono = "", string Correo = "");

public record CrearCuidadorRequest(
    [Required] string PacienteId,
    [Required] [StringLength(200)] string Nombre,
    [Required] [StringLength(100)] string Parentesco,
    [Required] [RegularExpression("^[0-9]{10}$", ErrorMessage = "El teléfono debe contener exactamente 10 dígitos numéricos")] string Telefono,
    [Required] [EmailAddress] string Correo);

public record ActualizarCuidadorRequest(
    [Required] [StringLength(200)] string Nombre,
    [Required] [StringLength(100)] string Parentesco,
    [Required] [RegularExpression("^[0-9]{10}$", ErrorMessage = "El teléfono debe contener exactamente 10 dígitos numéricos")] string Telefono,
    [Required] [EmailAddress] string Correo);

public record VincularCuidadorRequest(
    [Required] [StringLength(50)] string CodigoAcceso,
    [Required] [StringLength(100)] string Nombre,
    [Required] [EmailAddress] string Correo,
    [Required] [MinLength(8)] [StringLength(128)] string Password);

// ── Sensores ──────────────────────────────────────────────

public record LecturaSensorRequest(
    [Range(20, 300)] int PulsoBpm,
    [Range(30.0, 45.0)] double TemperaturaC,
    [Range(0.0, 100.0)] double SudoracionGsr,
    string? PacienteId = null);

public record EventoMetabolicoResponse(
    string Id, string NivelRiesgo, double ProbabilidadMl,
    string Descripcion, DateTime FechaEvento, bool Atendida);

public record AtenderEventoRequest(
    [Required] string CuidadorId);

public record TrackingGpsRequest(
    [Range(-180.0, 180.0)] double Longitud,
    [Range(-90.0, 90.0)] double Latitud,
    bool EsEmergencia,
    string? PacienteId = null);

public record TrackingResponse(
    double Longitud, double Latitud, DateTime Timestamp, bool EsEmergencia);

// ── Notificaciones ────────────────────────────────────────

public record NotificacionResponse(
    string Id, string Titulo, string Mensaje, bool Leida, DateTime FechaEnvio);

// ── Planes ────────────────────────────────────────────────

public record PlanResponse(
    string Id, string Nombre, decimal Precio, string PrecioMoneda,
    int LimitePacientes, int LimiteCuidadores, int DiasHistorial,
    bool GpsContinuo, bool AiConsole, string Descripcion);

// ── Usuarios Web ──────────────────────────────────────────

public record UpdatePerfilRequest(
    [Required] [StringLength(100)] string Nombre,
    [Required] [StringLength(100)] string ApellidoPaterno,
    [StringLength(100)] string ApellidoMaterno);

public record CambiarCorreoRequest(
    [Required] [EmailAddress] string NuevoCorreo);

// ── Pagos ─────────────────────────────────────────────────

public record CrearSesionPagoRequest(
    [Required] string PlanNombre,
    string? MetodoPago = null);

public record SimularPagoRequest(
    [Required] string PlanNombre);

public record PagoResponse(
    string Id, decimal Monto, string Moneda, string Estado,
    DateTime FechaPago, string MetodoPago);

// ── ML ────────────────────────────────────────────────────

// ── Dispositivos ──────────────────────────────────────────

public record VincularDispositivoRequest(
    [Required] [StringLength(200)] string Nombre,
    [Required] [StringLength(50)] string MacAddress,
    string? PacienteId = null);
