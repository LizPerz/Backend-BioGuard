using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 5: Notificaciones Push
/// ENDPOINT MÓVIL
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificacionesController : ControllerBase
{
    private readonly NotificacionService _notificacionService;

    public NotificacionesController(NotificacionService notificacionService)
    {
        _notificacionService = notificacionService;
    }

    // ── Consulta ──────────────────────────────────────────────

    /// <summary>
    /// GET /api/Notificaciones/by-paciente/{pacienteId} [MÓVIL]
    /// MÓDULO 5: Obtener notificaciones del paciente
    /// </summary>
    [HttpGet("by-paciente/{pacienteId}")]
    public async Task<IActionResult> ObtenerPorPaciente(string pacienteId)
    {
        var notificaciones = await _notificacionService.ObtenerPorPacienteAsync(pacienteId);
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
    public async Task<IActionResult> Crear([FromBody] CrearNotificacionRequest request)
    {
        var notificacion = await _notificacionService.CrearAsync(
            request.PacienteId, request.Titulo, request.Mensaje, request.Tipo,
            request.CuidadorId, request.UsuarioWebId);

        return Ok(new { NotificacionId = notificacion.Id, message = "Notificación creada" });
    }
}

public record CrearNotificacionRequest(
    string PacienteId, string Titulo, string Mensaje, string Tipo,
    string? CuidadorId = null, string? UsuarioWebId = null);
