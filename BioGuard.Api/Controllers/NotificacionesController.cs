using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using MongoDB.Driver;

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
    private readonly OwnershipHelper _ownershipHelper;

    public NotificacionesController(NotificacionService notificacionService, PacienteService pacienteService, IMongoDbContext db, ILogger<NotificacionesController> logger, OwnershipHelper ownershipHelper)
    {
        _notificacionService = notificacionService;
        _pacienteService = pacienteService;
        _db = db;
        _logger = logger;
        _ownershipHelper = ownershipHelper;
    }

    // ── Consulta ──────────────────────────────────────────────

    /// <summary>
    /// GET /api/Notificaciones [WEB]
    /// MÓDULO 5: Obtener todas las notificaciones del usuario logueado
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, currentUserId, role!))
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
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        if (currentUserId != usuarioId)
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

    // ── FCM ──────────────────────────────────────────────

    /// <summary>
    /// POST /api/Notificaciones/fcm [MÓVIL]
    /// Registrar token FCM para notificaciones push
    /// </summary>
    [HttpPost("fcm")]
    public async Task<IActionResult> RegistrarFcm([FromBody] RegistrarFcmRequest request)
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Registering FCM token for user {UsuarioId}, platform: {Plataforma}", usuarioId, request.Plataforma);

        var existing = await _db.FindFirstOrDefaultAsync(_db.FcmTokens, t => t.UsuarioId == usuarioId && t.Rol == role);
        if (existing != null)
        {
            var update = Builders<FcmToken>.Update
                .Set(t => t.Token, request.Token)
                .Set(t => t.Plataforma, request.Plataforma)
                .Set(t => t.Activo, true);
            await _db.FcmTokens.UpdateOneAsync(t => t.Id == existing.Id, update);
        }
        else
        {
            var fcm = new FcmToken
            {
                UsuarioId = usuarioId,
                Rol = role ?? "",
                Token = request.Token,
                Plataforma = request.Plataforma,
                Activo = true,
                FechaRegistro = DateTime.UtcNow
            };
            await _db.FcmTokens.InsertOneAsync(fcm);
        }

        _logger.LogInformation("FCM token registered for user {UsuarioId}", usuarioId);
        return Ok(new { message = "Token FCM registrado" });
    }

    // ── Gestión ───────────────────────────────────────────────

    /// <summary>
    /// PUT /api/Notificaciones/{id}/leer [MÓVIL]
    /// MÓDULO 5: Marcar notificación como leída
    /// </summary>
    [HttpPut("{id}/leer")]
    public async Task<IActionResult> MarcarLeida(string id)
    {
        var notificacion = await _db.FindFirstOrDefaultAsync(_db.Notificaciones, n => n.Id == id);
        if (notificacion == null)
        {
            _logger.LogWarning("Notification {Id} not found when marking as read", id);
            return NotFound();
        }

        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(notificacion.PacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed marking notification as read - user: {UserId}, notification: {Id}", usuarioId, id);
            return Forbid();
        }

        _logger.LogInformation("Marking notification {Id} as read", id);
        await _notificacionService.MarcarLeidaAsync(id);
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
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(request.PacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed creating notification - user: {UserId}, paciente: {PacienteId}", usuarioId, request.PacienteId);
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
        var notificacion = await _db.FindFirstOrDefaultAsync(_db.Notificaciones, n => n.Id == id);
        if (notificacion == null)
        {
            _logger.LogWarning("Notification {Id} not found when attempting to delete", id);
            return NotFound();
        }

        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(notificacion.PacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed deleting notification - user: {UserId}, notification: {Id}", usuarioId, id);
            return Forbid();
        }

        _logger.LogInformation("Deleting notification {Id}", id);
        await _notificacionService.EliminarAsync(id);
        return NoContent();
    }

}

public record CrearNotificacionRequest(
    [Required] string PacienteId,
    [Required][StringLength(200)] string Titulo,
    [Required][StringLength(2000)] string Mensaje,
    [Required][StringLength(50)] string Tipo,
    string? CuidadorId = null, string? UsuarioWebId = null);

public record RegistrarFcmRequest(
    [Required] string Token,
    [Required] [StringLength(50)] string Plataforma);
