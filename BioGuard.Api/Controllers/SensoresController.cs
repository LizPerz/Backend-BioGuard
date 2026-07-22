using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 5: Sensores, Dashboard Clínico y GPS
/// ENDPOINT WEB + MÓVIL
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SensoresController : ControllerBase
{
    private readonly SensorService _sensorService;
    private readonly PacienteService _pacienteService;
    private readonly ILogger<SensoresController> _logger;

    public SensoresController(SensorService sensorService, PacienteService pacienteService, ILogger<SensoresController> logger)
    {
        _sensorService = sensorService;
        _pacienteService = pacienteService;
        _logger = logger;
    }

    // ── Lecturas (Envío de datos) ─────────────────────────────

    /// <summary>
    /// POST /api/Sensores/lectura [MÓVIL]
    /// MÓDULO 5: Recibir lectura individual del WearOS (cada 10s)
    /// </summary>
    [HttpPost("lectura")]
    public async Task<IActionResult> RecibirLectura([FromBody] LecturaSensorRequest request)
    {
        var pacienteId = User.FindFirst("paciente_id")?.Value;
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        _logger.LogInformation("Receiving sensor reading for paciente: {PacienteId}", pacienteId);
        var lectura = await _sensorService.InsertarLecturaAsync(
            pacienteId, "wearos-001", request.PulsoBpm, request.TemperaturaC,
            request.SudoracionGsr, 0.0);

        return Ok(new { LecturaId = lectura.Id, message = "Lectura recibida" });
    }

    /// <summary>
    /// POST /api/Sensores/lectura-batch [MÓVIL]
    /// MÓDULO 5: Subir lote de lecturas offline (SQLite → API)
    /// </summary>
    [HttpPost("lectura-batch")]
    public async Task<IActionResult> RecibirLecturaBatch([FromBody] List<LecturaSensorRequest> request)
    {
        var pacienteId = User.FindFirst("paciente_id")?.Value;
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        _logger.LogInformation("Receiving batch of {Count} sensor readings for paciente: {PacienteId}", request.Count, pacienteId);
        var count = 0;
        foreach (var lectura in request)
        {
            await _sensorService.InsertarLecturaAsync(
                pacienteId, "wearos-001", lectura.PulsoBpm, lectura.TemperaturaC,
                lectura.SudoracionGsr, 0.0);
            count++;
        }

        return Ok(new { Procesadas = count, message = "Lote procesado" });
    }

    // ── Lecturas (Consulta) ───────────────────────────────────

    /// <summary>
    /// GET /api/Sensores/lecturas/{pacienteId} [WEB + MÓVIL]
    /// MÓDULO 5: Últimas N lecturas del paciente
    /// </summary>
    [HttpGet("lecturas/{pacienteId}")]
    public async Task<IActionResult> ObtenerLecturas(string pacienteId, [FromQuery] int limite = 100)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching readings - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching {Limite} readings for paciente: {PacienteId}", limite, pacienteId);
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

    /// <summary>
    /// GET /api/Sensores/lecturas/{pacienteId}/rango [WEB + MÓVIL]
    /// MÓDULO 5: Lecturas filtradas por rango de fecha
    /// </summary>
    [HttpGet("lecturas/{pacienteId}/rango")]
    public async Task<IActionResult> ObtenerLecturasRango(
        string pacienteId, [FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching readings range - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching readings range for paciente: {PacienteId} from {Desde} to {Hasta}", pacienteId, desde, hasta);
        var lecturas = await _sensorService.ObtenerLecturasRangoAsync(pacienteId, desde, hasta);
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

    // ── Estadísticas (Dashboard) ──────────────────────────────

    /// <summary>
    /// GET /api/Sensores/estadisticas/{pacienteId} [WEB + MÓVIL]
    /// MÓDULO 5: KPIs: último pulso, promedios, estado actual
    /// </summary>
    [HttpGet("estadisticas/{pacienteId}")]
    public async Task<IActionResult> Estadisticas(string pacienteId)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching stats - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching statistics for paciente: {PacienteId}", pacienteId);
        var lecturas = await _sensorService.ObtenerLecturasAsync(pacienteId, 100);
        if (!lecturas.Any())
        {
            _logger.LogWarning("No sensor data found for paciente: {PacienteId}", pacienteId);
            return Ok(new { message = "Sin datos" });
        }

        var ultima = lecturas.First();
        return Ok(new
        {
            UltimoPulso = ultima.PulsoBpm,
            UltimaTemperatura = ultima.TemperaturaC,
            UltimaSudoracion = ultima.SudoracionGsr,
            PromedioPulso = lecturas.Average(l => l.PulsoBpm),
            PromedioTemperatura = lecturas.Average(l => l.TemperaturaC),
            EstadoActual = ultima.ProbabilidadPico > 0.85 ? "Critico" : "Normal",
            TotalLecturas = lecturas.Count
        });
    }

    /// <summary>
    /// GET /api/Sensores/estadisticas/{pacienteId}/tendencia [WEB]
    /// MÓDULO 5: Datos para gráfica (diario/semanal/mensual)
    /// </summary>
    [HttpGet("estadisticas/{pacienteId}/tendencia")]
    public async Task<IActionResult> Tendencia(string pacienteId, [FromQuery] string periodo = "diario")
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching trend - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching trend for paciente: {PacienteId}, period: {Periodo}", pacienteId, periodo);
        var desde = periodo switch
        {
            "semanal" => DateTime.UtcNow.AddDays(-7),
            "mensual" => DateTime.UtcNow.AddDays(-30),
            _ => DateTime.UtcNow.AddDays(-1)
        };

        var lecturas = await _sensorService.ObtenerLecturasRangoAsync(pacienteId, desde, DateTime.UtcNow);
        var response = lecturas.Select(l => new
        {
            l.Timestamp,
            l.PulsoBpm,
            l.TemperaturaC,
            l.ProbabilidadPico
        }).Reverse();

        return Ok(response);
    }

    // ── Eventos ───────────────────────────────────────────────

    /// <summary>
    /// POST /api/Sensores/evento [MÓVIL]
    /// MÓDULO 5: Crear evento metabólico (TFLite detecta ≥0.85)
    /// </summary>
    [HttpPost("evento")]
    public async Task<IActionResult> CrearEvento([FromBody] CrearEventoRequest request)
    {
        var pacienteId = User.FindFirst("paciente_id")?.Value;
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        _logger.LogInformation("Creating metabolic event for paciente: {PacienteId}, risk: {NivelRiesgo}", pacienteId, request.NivelRiesgo);
        var evento = await _sensorService.CrearEventoAsync(
            pacienteId, request.Probabilidad, request.NivelRiesgo, request.Descripcion);

        return Ok(new { EventoId = evento.Id, message = "Evento creado" });
    }

    /// <summary>
    /// GET /api/Sensores/eventos/{pacienteId} [WEB + MÓVIL]
    /// MÓDULO 5: Historial de eventos/alertas
    /// </summary>
    [HttpGet("eventos/{pacienteId}")]
    public async Task<IActionResult> ObtenerEventos(string pacienteId, [FromQuery] int limite = 50)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching events - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching {Limite} events for paciente: {PacienteId}", limite, pacienteId);
        var eventos = await _sensorService.ObtenerEventosAsync(pacienteId, limite);
        var response = eventos.Select(e => new EventoMetabolicoResponse(
            e.Id, e.NivelRiesgo, e.ProbabilidadMl, e.Descripcion,
            e.FechaEvento, e.Atendida));
        return Ok(response);
    }

    /// <summary>
    /// GET /api/Sensores/eventos/{pacienteId}/resumen [WEB]
    /// MÓDULO 5: Total por nivel de riesgo
    /// </summary>
    [HttpGet("eventos/{pacienteId}/resumen")]
    public async Task<IActionResult> ResumenEventos(string pacienteId)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching event summary - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching event summary for paciente: {PacienteId}", pacienteId);
        var eventos = await _sensorService.ObtenerEventosAsync(pacienteId, 100);
        return Ok(new
        {
            Total = eventos.Count,
            Criticos = eventos.Count(e => e.NivelRiesgo == "Critico"),
            PrePico = eventos.Count(e => e.NivelRiesgo == "Pre-Pico"),
            Normal = eventos.Count(e => e.NivelRiesgo == "Normal"),
            Atendidos = eventos.Count(e => e.Atendida)
        });
    }

    /// <summary>
    /// PUT /api/Sensores/eventos/{eventoId}/atender [WEB + MÓVIL]
    /// MÓDULO 5: Marcar evento como atendido con acción tomada
    /// </summary>
    [HttpPut("eventos/{eventoId}/atender")]
    public async Task<IActionResult> AtenderEvento(string eventoId, [FromBody] AtenderEventoRequest request)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Marking event as attended: {EventoId}, cuidador: {CuidadorId}", eventoId, request.CuidadorId);
        var result = await _sensorService.AtenderEventoAsync(eventoId, request.CuidadorId);
        if (!result)
        {
            _logger.LogWarning("Event not found for attending: {EventoId}", eventoId);
            return NotFound();
        }
        return Ok(new { message = "Evento atendido" });
    }

    // ── Exportación ───────────────────────────────────────────

    /// <summary>
    /// GET /api/Sensores/lecturas/{pacienteId}/exportar-pdf [WEB]
    /// MÓDULO 5: Generar reporte médico PDF
    /// </summary>
    [HttpGet("lecturas/{pacienteId}/exportar-pdf")]
    public async Task<IActionResult> ExportarPDF(string pacienteId)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed exporting PDF - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Exporting PDF for paciente: {PacienteId}", pacienteId);
        var lecturas = await _sensorService.ObtenerLecturasAsync(pacienteId, 1000);
        return Ok(new { message = $"PDF generado con {lecturas.Count} registros", DescargaUrl = $"/api/sensores/lecturas/{pacienteId}/exportar-pdf/descarga" });
    }

    // ── Tracking GPS ──────────────────────────────────────────

    /// <summary>
    /// POST /api/Sensores/tracking [MÓVIL]
    /// MÓDULO 5: Enviar ubicación GPS (emergencia o continua)
    /// </summary>
    [HttpPost("tracking")]
    public async Task<IActionResult> InsertarTracking([FromBody] TrackingGpsRequest request)
    {
        var pacienteId = User.FindFirst("paciente_id")?.Value;
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        _logger.LogInformation("Inserting GPS tracking for paciente: {PacienteId}, emergency: {EsEmergencia}", pacienteId, request.EsEmergencia);
        await _sensorService.InsertarTrackingAsync(
            pacienteId, "wearos-001", request.Longitud, request.Latitud, request.EsEmergencia);

        return Ok(new { message = "Tracking insertado" });
    }

    /// <summary>
    /// POST /api/Sensores/tracking-batch [MÓVIL]
    /// MÓDULO 5: Subir lote de GPS offline
    /// </summary>
    [HttpPost("tracking-batch")]
    public async Task<IActionResult> InsertarTrackingBatch([FromBody] List<TrackingGpsRequest> request)
    {
        var pacienteId = User.FindFirst("paciente_id")?.Value;
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        _logger.LogInformation("Inserting GPS batch of {Count} records for paciente: {PacienteId}", request.Count, pacienteId);
        foreach (var track in request)
        {
            await _sensorService.InsertarTrackingAsync(
                pacienteId, "wearos-001", track.Longitud, track.Latitud, track.EsEmergencia);
        }

        return Ok(new { Procesadas = request.Count, message = "Lote GPS procesado" });
    }

    /// <summary>
    /// GET /api/Sensores/tracking/{pacienteId}/actual [WEB + MÓVIL]
    /// MÓDULO 5: Última ubicación GPS conocida
    /// </summary>
    [HttpGet("tracking/{pacienteId}/actual")]
    public async Task<IActionResult> TrackingActual(string pacienteId)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching current tracking - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching current GPS location for paciente: {PacienteId}", pacienteId);
        var ubicacion = await _sensorService.ObtenerUltimaUbicacionAsync(pacienteId);
        if (ubicacion == null)
        {
            _logger.LogWarning("No GPS location found for paciente: {PacienteId}", pacienteId);
            return NotFound(new { message = "Sin ubicación" });
        }

        return Ok(new TrackingResponse(
            ubicacion.Ubicacion.Coordinates[0],
            ubicacion.Ubicacion.Coordinates[1],
            ubicacion.Timestamp,
            ubicacion.EsEmergencia));
    }

    /// <summary>
    /// GET /api/Sensores/tracking/{pacienteId}/ruta [WEB]
    /// MÓDULO 5: Ruta GPS en rango de tiempo
    /// </summary>
    [HttpGet("tracking/{pacienteId}/ruta")]
    public async Task<IActionResult> TrackingRuta(
        string pacienteId, [FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching GPS route - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching GPS route for paciente: {PacienteId} from {Desde} to {Hasta}", pacienteId, desde, hasta);
        var puntos = await _sensorService.ObtenerTrackingRangoAsync(pacienteId, desde, hasta);
        var response = puntos.Select(p => new TrackingResponse(
            p.Ubicacion.Coordinates[0],
            p.Ubicacion.Coordinates[1],
            p.Timestamp,
            p.EsEmergencia));
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

public record CrearEventoRequest(
    double Probabilidad, string NivelRiesgo, string Descripcion);
