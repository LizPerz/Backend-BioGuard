using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;

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

    public DispositivosController(DispositivoService dispositivoService)
    {
        _dispositivoService = dispositivoService;
    }

    // ── Vinculación ───────────────────────────────────────────

    /// <summary>
    /// POST /api/Dispositivos/vincular [MÓVIL]
    /// MÓDULO 3: Registrar WearOS vinculado (MAC, nombre)
    /// </summary>
    [HttpPost("vincular")]
    public async Task<IActionResult> Vincular([FromBody] VincularDispositivoRequest request)
    {
        var pacienteId = User.FindFirst("paciente_id")?.Value;
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        var dispositivo = await _dispositivoService.VincularAsync(pacienteId, request.Nombre, request.MacAddress);
        if (dispositivo == null) return BadRequest(new { message = "Ya tiene un dispositivo vinculado" });

        return Ok(new { DispositivoId = dispositivo.Id, message = "Dispositivo vinculado" });
    }

    /// <summary>
    /// POST /api/Dispositivos/heartbeat [MÓVIL]
    /// MÓDULO 3: Keepalive del reloj (actualiza conectado=true)
    /// </summary>
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
    {
        var pacienteId = User.FindFirst("paciente_id")?.Value;
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        await _dispositivoService.HeartbeatAsync(pacienteId);
        return Ok(new { message = "Heartbeat recibido" });
    }

    // ── Consulta ──────────────────────────────────────────────

    /// <summary>
    /// GET /api/Dispositivos/{pacienteId} [MÓVIL]
    /// MÓDULO 3: Verificar si tiene reloj vinculado y estado
    /// </summary>
    [HttpGet("{pacienteId}")]
    public async Task<IActionResult> ObtenerPorPaciente(string pacienteId)
    {
        var dispositivo = await _dispositivoService.ObtenerPorPacienteAsync(pacienteId);
        if (dispositivo == null) return Ok(new { Vinculado = false });

        return Ok(new
        {
            Vinculado = true,
            dispositivo.NombreDispositivo,
            dispositivo.MacAddress,
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
        var pacienteId = User.FindFirst("paciente_id")?.Value;
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        var result = await _dispositivoService.ActualizarAsync(id, request.Nombre);
        if (!result) return NotFound();
        return Ok(new { message = "Dispositivo actualizado" });
    }

    /// <summary>
    /// DELETE /api/Dispositivos/{id} [MÓVIL]
    /// MÓDULO 3: Desvincular dispositivo
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Desvincular(string id)
    {
        var pacienteId = User.FindFirst("paciente_id")?.Value;
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        var result = await _dispositivoService.EliminarAsync(id);
        if (!result) return NotFound();
        return NoContent();
    }
}

public record HeartbeatRequest(string PacienteId);

public record ActualizarDispositivoRequest(
    [Required] [StringLength(200)] string Nombre);
