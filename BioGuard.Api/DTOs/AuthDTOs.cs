using System.ComponentModel.DataAnnotations;

namespace BioGuard.Api.DTOs;

// ── Auth ──────────────────────────────────────────────────

public record RegisterWebRequest(
    [Required] [StringLength(100)] string Nombre,
    [Required] [StringLength(100)] string ApellidoPaterno,
    [StringLength(100)] string ApellidoMaterno,
    [Required] [EmailAddress] string Correo,
    [Required] [MinLength(8)] [StringLength(128)] string Password,
    [Required] string PlanNombre);

public record LoginWebRequest(
    [Required] [EmailAddress] string Correo,
    [Required] string Password);

public record LoginGoogleRequest(
    [Required] string IdToken);

public record LoginCodigoRequest(
    [Required] [StringLength(50)] string CodigoAcceso);

public record AuthResponse(string Token, string UserId, string Nombre, string Rol, string Plan);

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

public record CambiarPasswordRequest(
    [Required] string PasswordActual,
    [Required] [MinLength(8)] [StringLength(128)] string NuevaPassword);

// ── Pacientes ─────────────────────────────────────────────

public record PacienteResponse(
    string Id, string Nombre, bool EsDiabetico,
    bool PerfilCompletado, string CodigoAccesoQr);

public record UpdateBiometriaRequest(
    [Range(0, 150)] int Edad,
    [Range(0.1, 500)] double PesoKg,
    [Range(20, 300)] double EstaturaCm,
    bool EsDiabetico, bool FamiliaresDiabetes,
    [StringLength(50)] string ActividadFisica);

public record CrearPacienteRequest(
    [Required] [StringLength(200)] string Nombre);
public record UpdateNombreRequest(
    [Required] [StringLength(200)] string Nombre);

// ── Cuidadores ────────────────────────────────────────────

public record CuidadorResponse(
    string Id, string Nombre, string Parentesco,
    string PacienteId, string CodigoAccesoQr);

public record CrearCuidadorRequest(
    [Required] string PacienteId,
    [Required] [StringLength(200)] string Nombre,
    [Required] [StringLength(100)] string Parentesco,
    [Required] [Phone] string Telefono,
    [Required] [EmailAddress] string Correo);

public record ActualizarCuidadorRequest(
    [Required] [StringLength(200)] string Nombre,
    [Required] [StringLength(100)] string Parentesco);

// ── Sensores ──────────────────────────────────────────────

public record LecturaSensorRequest(
    [Range(20, 300)] int PulsoBpm,
    [Range(30.0, 45.0)] double TemperaturaC,
    [Range(0.0, 100.0)] double SudoracionGsr);

public record EventoMetabolicoResponse(
    string Id, string NivelRiesgo, double ProbabilidadMl,
    string Descripcion, DateTime FechaEvento, bool Atendida);

public record AtenderEventoRequest(
    [Required] string CuidadorId);
public record AgregarAccionRequest(
    [Required] [StringLength(500)] string Accion);

public record TrackingGpsRequest(
    [Range(-180.0, 180.0)] double Longitud,
    [Range(-90.0, 90.0)] double Latitud,
    bool EsEmergencia);

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
    [Required] string PlanNombre);

public record PagoResponse(
    string Id, decimal Monto, string Moneda, string Estado,
    DateTime FechaPago, string MetodoPago);

// ── ML ────────────────────────────────────────────────────

public record PrediccionResponse(
    string Id, double Probabilidad, string NivelRiesgo,
    string Recomendacion, DateTime FechaPrediccion);

public record ModeloMlResponse(
    string Id, string Nombre, string Tipo,
    double Accuracy, bool Activo, DateTime FechaEntrenamiento);

// ── Dispositivos ──────────────────────────────────────────

public record VincularDispositivoRequest(
    [Required] [StringLength(200)] string Nombre,
    [Required] [StringLength(50)] string MacAddress);
