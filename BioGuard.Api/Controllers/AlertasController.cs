using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 5: Alertas Críticas de Sensores
/// ENDPOINT WEB + MÓVIL
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlertasController : ControllerBase
{
    private readonly AlertaService _alertaService;
    private readonly PacienteService _pacienteService;

    public AlertasController(AlertaService alertaService, PacienteService pacienteService)
    {
        _alertaService = alertaService;
        _pacienteService = pacienteService;
    }

    /// <summary>
    /// GET /api/Alertas/by-paciente/{pacienteId} [WEB + MÓVIL]
    /// </summary>
    [HttpGet("by-paciente/{pacienteId}")]
    public async Task<IActionResult> ObtenerPorPaciente(string pacienteId, [FromQuery] int limite = 50)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
            return Forbid();

        var alertas = await _alertaService.ObtenerPorPacienteAsync(pacienteId, limite);
        var response = alertas.Select(a => new AlertaResponse(
            a.Id, a.PacienteId, a.Tipo, a.Nivel, a.Titulo, a.Mensaje,
            a.Atendida, a.FechaCreacion, a.FechaAtencion));
        return Ok(response);
    }

    /// <summary>
    /// GET /api/Alertas/pendientes/{pacienteId} [WEB + MÓVIL]
    /// </summary>
    [HttpGet("pendientes/{pacienteId}")]
    public async Task<IActionResult> ObtenerPendientes(string pacienteId)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
            return Forbid();

        var alertas = await _alertaService.ObtenerPendientesAsync(pacienteId);
        var response = alertas.Select(a => new AlertaResponse(
            a.Id, a.PacienteId, a.Tipo, a.Nivel, a.Titulo, a.Mensaje,
            a.Atendida, a.FechaCreacion, a.FechaAtencion));
        return Ok(response);
    }

    /// <summary>
    /// GET /api/Alertas/{id} [WEB + MÓVIL]
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var alerta = await _alertaService.ObtenerPorIdAsync(id);
        if (alerta == null) return NotFound();

        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(alerta.PacienteId, usuarioId, role!))
            return Forbid();

        return Ok(new AlertaResponse(
            alerta.Id, alerta.PacienteId, alerta.Tipo, alerta.Nivel,
            alerta.Titulo, alerta.Mensaje, alerta.Atendida,
            alerta.FechaCreacion, alerta.FechaAtencion));
    }

    /// <summary>
    /// POST /api/Alertas [MÓVIL/ML]
    /// Crear alerta crítica (triggered by ML o sensores)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearAlertaRequest request)
    {
        var sensorData = new SensorData
        {
            PulsoBpm = request.PulsoBpm,
            TemperaturaC = request.TemperaturaC,
            SudoracionGsr = request.SudoracionGsr,
            ProbabilidadPico = request.ProbabilidadPico
        };

        var alerta = await _alertaService.CrearAsync(
            request.PacienteId, request.Tipo, request.Nivel,
            request.Titulo, request.Mensaje, sensorData);

        return Ok(new { AlertaId = alerta.Id, message = "Alerta creada" });
    }

    /// <summary>
    /// PUT /api/Alertas/{id}/resolver [WEB + MÓVIL]
    /// </summary>
    [HttpPut("{id}/resolver")]
    public async Task<IActionResult> Resolver(string id, [FromBody] ResolverAlertaRequest request)
    {
        var result = await _alertaService.ResolverAsync(id, request.CuidadorId, request.AccionTomada);
        if (!result) return NotFound();
        return Ok(new { message = "Alerta resuelta" });
    }

    /// <summary>
    /// DELETE /api/Alertas/{id} [WEB]
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Eliminar(string id)
    {
        var alerta = await _alertaService.ObtenerPorIdAsync(id);
        if (alerta == null) return NotFound();

        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        var paciente = await _pacienteService.GetByIdAsync(alerta.PacienteId);
        if (paciente?.UsuarioWebId != usuarioId) return Forbid();

        var result = await _alertaService.EliminarAsync(id);
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
