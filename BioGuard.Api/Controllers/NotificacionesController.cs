using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;
using BioGuard.Api.Config;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 5: Notificaciones Push
/// ENDPOINT WEB + MÓVIL
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificacionesController : ControllerBase
{
    private readonly NotificacionService _notificacionService;
    private readonly PacienteService _pacienteService;
    private readonly IMongoDbContext _db;
    private readonly ILogger<NotificacionesController> _logger;

    public NotificacionesController(NotificacionService notificacionService, PacienteService pacienteService, IMongoDbContext db, ILogger<NotificacionesController> logger)
    {
        _notificacionService = notificacionService;
        _pacienteService = pacienteService;
        _db = db;
        _logger = logger;
    }

    // ── Consulta ──────────────────────────────────────────────

    /// <summary>
    /// GET /api/Notificaciones [WEB]
    /// MÓDULO 5: Obtener todas las notificaciones del usuario logueado
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Listing notifications for user {UsuarioId}", usuarioId);
        var notificaciones = await _notificacionService.ObtenerPorUsuarioAsync(usuarioId);
        var response = notificaciones.Select(n => new NotificacionResponse(
            n.Id, n.Titulo, n.Mensaje, n.Leida, n.FechaEnvio));
        return Ok(response);
    }

    /// <summary>
    /// GET /api/Notificaciones/by-paciente/{pacienteId} [MÓVIL]
    /// MÓDULO 5: Obtener notificaciones del paciente
    /// </summary>
    [HttpGet("by-paciente/{pacienteId}")]
    public async Task<IActionResult> ObtenerPorPaciente(string pacienteId)
    {
        var currentUserId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, currentUserId, role!))
        {
            _logger.LogWarning("Ownership check failed for patient {PacienteId} requested by user {UsuarioId}", pacienteId, currentUserId);
            return Forbid();
        }

        _logger.LogInformation("Listing notifications for patient {PacienteId}", pacienteId);
        var notificaciones = await _notificacionService.ObtenerPorPacienteAsync(pacienteId);
        var response = notificaciones.Select(n => new NotificacionResponse(
            n.Id, n.Titulo, n.Mensaje, n.Leida, n.FechaEnvio));
        return Ok(response);
    }

    /// <summary>
    /// GET /api/Notificaciones/by-usuario/{usuarioId} [WEB]
    /// MÓDULO 5: Obtener notificaciones por usuario web
    /// </summary>
    [HttpGet("by-usuario/{usuarioId}")]
    public async Task<IActionResult> ObtenerPorUsuario(string usuarioId)
    {
        var currentUserId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        if (role == "dueno" && currentUserId != usuarioId)
        {
            _logger.LogWarning("User {UsuarioId} attempted to access notifications of user {TargetUsuarioId} without permission", currentUserId, usuarioId);
            return Forbid();
        }

        _logger.LogInformation("Listing notifications for user {UsuarioId}", usuarioId);
        var notificaciones = await _notificacionService.ObtenerPorUsuarioAsync(usuarioId);
        var response = notificaciones.Select(n => new NotificacionResponse(
            n.Id, n.Titulo, n.Mensaje, n.Leida, n.FechaEnvio));
        return Ok(response);
    }

    // ── Gestión ───────────────────────────────────────────────

    /// <summary>
    /// PUT /api/Notificaciones/{id}/leer [MÓVIL]
    /// MÓDULO 5: Marcar notificación como leída
    /// </summary>
    [HttpPut("{id}/leer")]
    public async Task<IActionResult> MarcarLeida(string id)
    {
        _logger.LogInformation("Marking notification {Id} as read", id);
        var result = await _notificacionService.MarcarLeidaAsync(id);
        if (!result)
        {
            _logger.LogWarning("Notification {Id} not found when marking as read", id);
            return NotFound();
        }
        return Ok(new { message = "Notificación marcada como leída" });
    }

    // ── Envío (interno) ──────────────────────────────────────

    /// <summary>
    /// POST /api/Notificaciones [MÓVIL]
    /// MÓDULO 5: Crear notificación + enviar por FCM
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "dueno,paciente")]
    public async Task<IActionResult> Crear([FromBody] CrearNotificacionRequest request)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (role == "paciente" && usuarioId != request.PacienteId)
        {
            _logger.LogWarning("Patient {UsuarioId} attempted to create notification for different patient {PacienteId}", usuarioId, request.PacienteId);
            return Forbid();
        }

        _logger.LogInformation("Creating notification for patient {PacienteId} by user {UsuarioId}", request.PacienteId, usuarioId);
        var notificacion = await _notificacionService.CrearAsync(
            request.PacienteId, request.Titulo, request.Mensaje, request.Tipo,
            request.CuidadorId, request.UsuarioWebId);

        return Ok(new { NotificacionId = notificacion.Id, message = "Notificación creada" });
    }

    /// <summary>
    /// DELETE /api/Notificaciones/{id} [WEB]
    /// MÓDULO 5: Eliminar notificación
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(string id)
    {
        _logger.LogInformation("Deleting notification {Id}", id);
        var result = await _notificacionService.EliminarAsync(id);
        if (!result)
        {
            _logger.LogWarning("Notification {Id} not found when attempting to delete", id);
            return NotFound();
        }
        return NoContent();
    }

    private async Task<bool> VerifyPacienteOwnership(string pacienteId, string userId, string role)
    {
        if (role == "paciente") return pacienteId == userId;
        if (role == "cuidador")
        {
            var cuidador = await _db.FindFirstOrDefaultAsync(_db.Cuidadores, c => c.UsuarioWebId == userId && c.PacienteId == pacienteId);
            return cuidador != null;
        }

        var paciente = await _pacienteService.GetByIdAsync(pacienteId);
        return paciente?.UsuarioWebId == userId;
    }
}

public record CrearNotificacionRequest(
    string PacienteId, string Titulo, string Mensaje, string Tipo,
    string? CuidadorId = null, string? UsuarioWebId = null);
