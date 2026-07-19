using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;

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

    public NotificacionesController(NotificacionService notificacionService, PacienteService pacienteService)
    {
        _notificacionService = notificacionService;
        _pacienteService = pacienteService;
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
            return Forbid();

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
            return Forbid();

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
        var result = await _notificacionService.MarcarLeidaAsync(id);
        if (!result) return NotFound();
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
            return Forbid();

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
        var result = await _notificacionService.EliminarAsync(id);
        if (!result) return NotFound();
        return NoContent();
    }

    private async Task<bool> VerifyPacienteOwnership(string pacienteId, string userId, string role)
    {
        if (role == "paciente") return pacienteId == userId;
        if (role == "cuidador") return true;

        var paciente = await _pacienteService.GetByIdAsync(pacienteId);
        return paciente?.UsuarioWebId == userId;
    }
}

public record CrearNotificacionRequest(
    string PacienteId, string Titulo, string Mensaje, string Tipo,
    string? CuidadorId = null, string? UsuarioWebId = null);
