using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 7: Reportes (resumen + historial)
/// ENDPOINT WEB
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportesController : ControllerBase
{
    private readonly SensorService _sensorService;
    private readonly AlertaService _alertaService;
    private readonly MedicamentoService _medicamentoService;
    private readonly PacienteService _pacienteService;

    public ReportesController(
        SensorService sensorService,
        AlertaService alertaService,
        MedicamentoService medicamentoService,
        PacienteService pacienteService)
    {
        _sensorService = sensorService;
        _alertaService = alertaService;
        _medicamentoService = medicamentoService;
        _pacienteService = pacienteService;
    }

    /// <summary>
    /// GET /api/Reportes/resumen/{pacienteId} [WEB]
    /// KPIs generales del paciente
    /// </summary>
    [HttpGet("resumen/{pacienteId}")]
    public async Task<IActionResult> Resumen(string pacienteId)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
            return Forbid();

        var lecturas = await _sensorService.ObtenerLecturasAsync(pacienteId, 1000);
        var eventos = await _sensorService.ObtenerEventosAsync(pacienteId, 1000);
        var alertas = await _alertaService.ObtenerPorPacienteAsync(pacienteId, 1000);
        var medicamentos = await _medicamentoService.ObtenerPorPacienteAsync(pacienteId);

        var promedioPulso = lecturas.Count > 0
            ? lecturas.Average(l => l.PulsoBpm)
            : 0.0;

        var response = new ReporteResumenResponse(
            lecturas.Count,
            eventos.Count,
            alertas.Count,
            medicamentos.Count,
            eventos.Count(e => e.NivelRiesgo == "Critico"),
            alertas.Count(a => !a.Atendida),
            promedioPulso,
            lecturas.FirstOrDefault()?.Timestamp);

        return Ok(response);
    }

    /// <summary>
    /// GET /api/Reportes/historial-alertas/{pacienteId} [WEB]
    /// </summary>
    [HttpGet("historial-alertas/{pacienteId}")]
    public async Task<IActionResult> HistorialAlertas(string pacienteId, [FromQuery] int limite = 100)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
            return Forbid();

        var alertas = await _alertaService.ObtenerPorPacienteAsync(pacienteId, limite);
        var response = alertas.Select(a => new ReporteAlertaResponse(
            a.Id, a.Tipo, a.Nivel, a.Titulo, a.Mensaje,
            a.Atendida, a.FechaCreacion, a.FechaAtencion));
        return Ok(response);
    }

    /// <summary>
    /// GET /api/Reportes/historial-eventos/{pacienteId} [WEB]
    /// </summary>
    [HttpGet("historial-eventos/{pacienteId}")]
    public async Task<IActionResult> HistorialEventos(string pacienteId, [FromQuery] int limite = 100)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
            return Forbid();

        var eventos = await _sensorService.ObtenerEventosAsync(pacienteId, limite);
        var response = eventos.Select(e => new ReporteEventoResponse(
            e.Id, e.NivelRiesgo, e.ProbabilidadMl, e.Descripcion,
            e.FechaEvento, e.Atendida));
        return Ok(response);
    }

    /// <summary>
    /// GET /api/Reportes/historial-medicamentos/{pacienteId} [WEB]
    /// </summary>
    [HttpGet("historial-medicamentos/{pacienteId}")]
    public async Task<IActionResult> HistorialMedicamentos(string pacienteId)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
            return Forbid();

        var medicamentos = await _medicamentoService.ObtenerPorPacienteAsync(pacienteId);
        var response = medicamentos.Select(m => new ReporteMedicamentoResponse(
            m.Id, m.Nombre, m.Dosis, m.Horario, m.Activo, m.UltimaToma));
        return Ok(response);
    }

    /// <summary>
    /// GET /api/Reportes/historial-lecturas/{pacienteId} [WEB]
    /// </summary>
    [HttpGet("historial-lecturas/{pacienteId}")]
    public async Task<IActionResult> HistorialLecturas(
        string pacienteId, [FromQuery] int limite = 500)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
            return Forbid();

        var lecturas = await _sensorService.ObtenerLecturasAsync(pacienteId, limite);
        var response = lecturas.Select(l => new
        {
            l.Id,
            l.PulsoBpm,
            l.TemperaturaC,
            l.SudoracionGsr,
            l.ProbabilidadPico,
            l.Timestamp
        });
        return Ok(response);
    }

    private async Task<bool> VerifyPacienteOwnership(string pacienteId, string userId, string role)
    {
        if (role == "paciente") return pacienteId == userId;
        if (role == "cuidador") return true;

        var paciente = await _pacienteService.GetByIdAsync(pacienteId);
        return paciente?.UsuarioWebId == userId;
    }
}
