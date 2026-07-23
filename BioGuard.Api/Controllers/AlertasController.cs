using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;
using BioGuard.Api.Config;

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
    private readonly IMongoDbContext _db;
    private readonly ILogger<AlertasController> _logger;

    public AlertasController(AlertaService alertaService, PacienteService pacienteService, IMongoDbContext db, ILogger<AlertasController> logger)
    {
        _alertaService = alertaService;
        _pacienteService = pacienteService;
        _db = db;
        _logger = logger;
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
        {
            _logger.LogWarning("Ownership check failed fetching alerts - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching alerts for paciente: {PacienteId}, limit: {Limite}", pacienteId, limite);
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
        {
            _logger.LogWarning("Ownership check failed fetching pending alerts - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching pending alerts for paciente: {PacienteId}", pacienteId);
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
        _logger.LogInformation("Fetching alert by ID: {AlertaId}", id);
        var alerta = await _alertaService.ObtenerPorIdAsync(id);
        if (alerta == null)
        {
            _logger.LogWarning("Alert not found: {AlertaId}", id);
            return NotFound();
        }

        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(alerta.PacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching alert - user: {UserId}, paciente: {PacienteId}", usuarioId, alerta.PacienteId);
            return Forbid();
        }

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
        _logger.LogInformation("Creating alert for paciente: {PacienteId}, type: {Tipo}, level: {Nivel}", request.PacienteId, request.Tipo, request.Nivel);
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

        _logger.LogInformation("Alert created successfully: {AlertaId}", alerta.Id);
        return Ok(new { AlertaId = alerta.Id, message = "Alerta creada" });
    }

    /// <summary>
    /// PUT /api/Alertas/{id}/resolver [WEB + MÓVIL]
    /// </summary>
    [HttpPut("{id}/resolver")]
    public async Task<IActionResult> Resolver(string id, [FromBody] ResolverAlertaRequest request)
    {
        _logger.LogInformation("Resolving alert: {AlertaId}, cuidador: {CuidadorId}", id, request.CuidadorId);
        var result = await _alertaService.ResolverAsync(id, request.CuidadorId, request.AccionTomada);
        if (!result)
        {
            _logger.LogWarning("Alert not found for resolution: {AlertaId}", id);
            return NotFound();
        }
        _logger.LogInformation("Alert resolved successfully: {AlertaId}", id);
        return Ok(new { message = "Alerta resuelta" });
    }

    /// <summary>
    /// DELETE /api/Alertas/{id} [WEB]
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Eliminar(string id)
    {
        _logger.LogInformation("Deleting alert: {AlertaId}", id);
        var alerta = await _alertaService.ObtenerPorIdAsync(id);
        if (alerta == null)
        {
            _logger.LogWarning("Alert not found for deletion: {AlertaId}", id);
            return NotFound();
        }

        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        var paciente = await _pacienteService.GetByIdAsync(alerta.PacienteId);
        if (paciente?.UsuarioWebId != usuarioId)
        {
            _logger.LogWarning("Ownership check failed deleting alert - user: {UserId}, paciente: {PacienteId}", usuarioId, alerta.PacienteId);
            return Forbid();
        }

        var result = await _alertaService.EliminarAsync(id);
        if (!result)
        {
            _logger.LogWarning("Alert deletion failed: {AlertaId}", id);
            return NotFound();
        }
        _logger.LogInformation("Alert deleted successfully: {AlertaId}", id);
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
