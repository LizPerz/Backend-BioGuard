using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;
using BioGuard.Api.Config;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 3: Dispositivos WearOS (Hardware)
/// ENDPOINT MÓVIL
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DispositivosController : ControllerBase
{
    private readonly DispositivoService _dispositivoService;
    private readonly OwnershipHelper _ownershipHelper;
    private readonly ILogger<DispositivosController> _logger;

    public DispositivosController(DispositivoService dispositivoService, OwnershipHelper ownershipHelper, ILogger<DispositivosController> logger)
    {
        _dispositivoService = dispositivoService;
        _ownershipHelper = ownershipHelper;
        _logger = logger;
    }

    // ── Vinculación ───────────────────────────────────────────

    /// <summary>
    /// POST /api/Dispositivos/vincular [MÓVIL]
    /// MÓDULO 3: Registrar WearOS vinculado (MAC, nombre)
    /// </summary>
    [HttpPost("vincular")]
    public async Task<IActionResult> Vincular([FromBody] VincularDispositivoRequest request)
    {
        var pacienteId = await ResolverPacienteIdAsync(request.PacienteId);
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        _logger.LogInformation("Linking device for patient {PacienteId}", pacienteId);
        var dispositivo = await _dispositivoService.VincularAsync(pacienteId, request.Nombre, request.MacAddress);
        if (dispositivo == null)
        {
            _logger.LogWarning("Patient {PacienteId} already has a linked device", pacienteId);
            return BadRequest(new { message = "Ya tiene un dispositivo vinculado" });
        }

        return Ok(new { DispositivoId = dispositivo.Id, message = "Dispositivo vinculado" });
    }

    /// <summary>
    /// POST /api/Dispositivos/heartbeat [MÓVIL]
    /// MÓDULO 3: Keepalive del reloj (actualiza conectado=true)
    /// </summary>
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
    {
        var pacienteId = await ResolverPacienteIdAsync(request.PacienteId);
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        _logger.LogDebug("Heartbeat received for patient {PacienteId}", pacienteId);
        await _dispositivoService.HeartbeatAsync(pacienteId);
        return Ok(new { message = "Heartbeat recibido" });
    }

    // ── Consulta ──────────────────────────────────────────────

    /// <summary>
    /// Resuelve el pacienteId de la petición:
    /// 1. Prioriza el claim JWT "paciente_id" (emitido en login-codigo, rol paciente).
    /// 2. Si no existe (login-web, rol dueno/cuidador), usa el pacienteId del body
    ///    y valida que el usuario tenga ownership sobre ese paciente.
    /// </summary>
    private async Task<string?> ResolverPacienteIdAsync(string? bodyPacienteId)
    {
        var claimPacienteId = User.FindFirst("paciente_id")?.Value;
        if (!string.IsNullOrEmpty(claimPacienteId)) return claimPacienteId;

        if (string.IsNullOrEmpty(bodyPacienteId)) return null;

        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId) || string.IsNullOrEmpty(role)) return null;

        return await _ownershipHelper.VerifyPacienteOwnershipAsync(bodyPacienteId, usuarioId, role)
            ? bodyPacienteId
            : null;
    }

    /// <summary>
    /// GET /api/Dispositivos/{pacienteId} [MÓVIL]
    /// MÓDULO 3: Verificar si tiene reloj vinculado y estado
    /// </summary>
    [HttpGet("{pacienteId}")]
    public async Task<IActionResult> ObtenerPorPaciente(string pacienteId)
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed getting device - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Getting device for patient {PacienteId}", pacienteId);
        var dispositivo = await _dispositivoService.ObtenerPorPacienteAsync(pacienteId);
        if (dispositivo == null) return Ok(new { Vinculado = false });

        return Ok(new
        {
            Vinculado = true,
            dispositivo.NombreDispositivo,
            MacAddress = "XX:XX:XX:XX:XX:XX",
            dispositivo.Conectado,
            dispositivo.FechaVinculacion
        });
    }

    /// <summary>
    /// PUT /api/Dispositivos/{id} [MÓVIL]
    /// MÓDULO 3: Actualizar nombre del dispositivo
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(string id, [FromBody] ActualizarDispositivoRequest request)
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        var dispositivo = await _dispositivoService.ObtenerPorIdAsync(id);
        if (dispositivo == null) return NotFound();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(dispositivo.PacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed updating device - user: {UserId}, device: {DeviceId}", usuarioId, id);
            return Forbid();
        }

        _logger.LogInformation("Updating device {Id} name", id);
        var result = await _dispositivoService.ActualizarAsync(id, request.Nombre);
        if (!result)
        {
            _logger.LogWarning("Device {Id} not found when attempting to update", id);
            return NotFound();
        }
        return Ok(new { message = "Dispositivo actualizado" });
    }

    /// <summary>
    /// DELETE /api/Dispositivos/{id} [MÓVIL]
    /// MÓDULO 3: Desvincular dispositivo
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Desvincular(string id)
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        var dispositivo = await _dispositivoService.ObtenerPorIdAsync(id);
        if (dispositivo == null) return NotFound();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(dispositivo.PacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed unlinking device - user: {UserId}, device: {DeviceId}", usuarioId, id);
            return Forbid();
        }

        _logger.LogInformation("Unlinking device {Id}", id);
        var result = await _dispositivoService.EliminarAsync(id);
        if (!result)
        {
            _logger.LogWarning("Device {Id} not found when attempting to unlink", id);
            return NotFound();
        }
        return NoContent();
    }
}

public record HeartbeatRequest(string PacienteId);

public record ActualizarDispositivoRequest(
    [Required] [StringLength(200)] string Nombre);
