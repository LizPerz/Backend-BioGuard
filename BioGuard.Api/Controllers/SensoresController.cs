using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

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
    private readonly IMongoDbContext _db;
    private readonly AuditoriaService _auditoriaService;
    private readonly MLService _mlService;
    private readonly INotificacionMlService _notificacionService;
    private readonly ILogger<SensoresController> _logger;
    private readonly OwnershipHelper _ownershipHelper;

    public SensoresController(SensorService sensorService, PacienteService pacienteService, IMongoDbContext db, AuditoriaService auditoriaService, MLService mlService, INotificacionMlService notificacionService, ILogger<SensoresController> logger, OwnershipHelper ownershipHelper)
    {
        _sensorService = sensorService;
        _pacienteService = pacienteService;
        _db = db;
        _auditoriaService = auditoriaService;
        _mlService = mlService;
        _notificacionService = notificacionService;
        _logger = logger;
        _ownershipHelper = ownershipHelper;
    }

    // ── Lecturas (Envío de datos) ─────────────────────────────

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
    /// POST /api/Sensores/lectura [MÓVIL]
    /// MÓDULO 5: Recibir lectura individual del WearOS (cada 10s)
    /// El riesgo (ProbabilidadPico) lo calcula el móvil en local con el motor F1-F3.
    /// </summary>
    [HttpPost("lectura")]
    public async Task<IActionResult> RecibirLectura([FromBody] LecturaSensorRequest request)
    {
        var pacienteId = await ResolverPacienteIdAsync(request.PacienteId);
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        _logger.LogInformation("Receiving sensor reading for paciente: {PacienteId}", pacienteId);
        var lectura = await _sensorService.InsertarLecturaAsync(
            pacienteId, "wearos-001", request.PulsoBpm, request.TemperaturaC,
            request.SudoracionGsr, Math.Clamp(request.ProbabilidadPico ?? 0.0, 0.0, 1.0));

        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _auditoriaService.RegistrarAsync(usuarioId, "insertar_lectura", "lecturas_sensores", lectura.Id, ip);

        return Ok(new { LecturaId = lectura.Id, message = "Lectura recibida" });
    }

    /// <summary>
    /// POST /api/Sensores/lectura-batch [MÓVIL]
    /// MÓDULO 5: Subir lote de lecturas offline (SQLite → API)
    /// </summary>
    [HttpPost("lectura-batch")]
    [RequestSizeLimit(10485760)]
    public async Task<IActionResult> RecibirLecturaBatch([FromBody] List<LecturaSensorRequest> request)
    {
        var pacienteId = await ResolverPacienteIdAsync(request.FirstOrDefault()?.PacienteId);
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        _logger.LogInformation("Receiving batch of {Count} sensor readings for paciente: {PacienteId}", request.Count, pacienteId);
        var count = 0;
        foreach (var lectura in request)
        {
            await _sensorService.InsertarLecturaAsync(
                pacienteId, "wearos-001", lectura.PulsoBpm, lectura.TemperaturaC,
                lectura.SudoracionGsr, Math.Clamp(lectura.ProbabilidadPico ?? 0.0, 0.0, 1.0));
            count++;
        }

        return Ok(new { Procesadas = count, message = "Lote procesado" });
    }

    // ── Reportes de pico glucémico (cálculo local del móvil) ──

    /// <summary>
    /// POST /api/Sensores/prediccion [MÓVIL]
    /// Guarda el reporte calculado en local (IMC, z, P(Pico), caso clínico, acción).
    /// El backend solo persiste; la web lo pinta.
    /// </summary>
    [HttpPost("prediccion")]
    public async Task<IActionResult> GuardarReporte([FromBody] GuardarPrediccionRequest request)
    {
        var pacienteId = await ResolverPacienteIdAsync(request.PacienteId);
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        _logger.LogInformation("Saving glycemic peak report for paciente: {PacienteId}, caso: {CasoClinico}", pacienteId, request.CasoClinico);
        var entidad = new PrediccionMl
        {
            PacienteId = pacienteId,
            ProbabilidadPico = Math.Clamp(request.ProbabilidadPico, 0.0, 1.0),
            NivelRiesgo = request.NivelRiesgo,
            HorasEstimadas = request.HorasEstimadas,
            Recomendacion = request.Recomendacion ?? string.Empty,
            ModeloVersion = string.IsNullOrWhiteSpace(request.ModeloVersion) ? "pico-v1.0" : request.ModeloVersion,
            Imc = request.Imc,
            Z = request.Z,
            PPico = request.PPico,
            CasoClinico = request.CasoClinico ?? string.Empty,
            AccionAutomatizada = request.AccionAutomatizada ?? string.Empty,
            FechaPrediccion = DateTime.UtcNow,
            FechaExpiracion = DateTime.UtcNow.AddHours(2)
        };

        var guardada = await _mlService.GuardarPrediccionAsync(entidad);

        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _auditoriaService.RegistrarAsync(usuarioId, "guardar_prediccion", "predicciones_ml", guardada.Id, ip);

        return Ok(new { PrediccionId = guardada.Id, message = "Reporte guardado" });
    }

    /// <summary>
    /// GET /api/Sensores/predicciones/{pacienteId} [WEB + MÓVIL]
    /// Historial de reportes de pico glucémico.
    /// </summary>
    [HttpGet("predicciones/{pacienteId}")]
    public async Task<IActionResult> ObtenerPredicciones(string pacienteId)
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching predictions - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching predictions for paciente: {PacienteId}", pacienteId);
        var predicciones = await _mlService.ObtenerPrediccionesAsync(pacienteId);
        var response = predicciones.Select(p => new
        {
            p.Id,
            p.PacienteId,
            Probabilidad = p.ProbabilidadPico,
            p.NivelRiesgo,
            p.Recomendacion,
            p.FechaPrediccion,
            p.HorasEstimadas,
            p.ModeloVersion,
            p.Imc,
            p.Z,
            PPico = p.PPico,
            p.CasoClinico,
            p.AccionAutomatizada
        });
        return Ok(response);
    }

    /// <summary>
    /// GET /api/Sensores/predicciones/{pacienteId}/actual [WEB + MÓVIL]
    /// Reporte vigente ("próximas 2 horas").
    /// </summary>
    [HttpGet("predicciones/{pacienteId}/actual")]
    public async Task<IActionResult> PrediccionActual(string pacienteId)
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching current prediction - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching current prediction for paciente: {PacienteId}", pacienteId);
        var prediccion = await _mlService.ObtenerPrediccionActualAsync(pacienteId);
        if (prediccion == null)
        {
            _logger.LogWarning("No active prediction for patient {PacienteId}", pacienteId);
            return Ok(new { message = "Sin predicción activa" });
        }
        return Ok(new
        {
            prediccion.Id,
            prediccion.PacienteId,
            Probabilidad = prediccion.ProbabilidadPico,
            prediccion.NivelRiesgo,
            prediccion.Recomendacion,
            prediccion.FechaPrediccion,
            prediccion.HorasEstimadas,
            prediccion.ModeloVersion,
            prediccion.Imc,
            prediccion.Z,
            PPico = prediccion.PPico,
            prediccion.CasoClinico,
            prediccion.AccionAutomatizada
        });
    }

    // ── Predicciones ML (Guardar reportes) ────────────────────────

    /// <summary>
    /// POST /api/Sensores/prediccion [MÓVIL]
    /// MÓDULO 5: Guardar reporte de predicción ML local (F1-F3 motor)
    /// El móvil calcula localmente: IMC, z-score, P(Pico), clasificación de riesgo
    /// Este endpoint persiste el reporte para historial y web
    /// </summary>
    [HttpPost("prediccion")]
    public async Task<IActionResult> GuardarPrediccion([FromBody] GuardarPrediccionRequest request)
    {
        var pacienteId = await ResolverPacienteIdAsync(request.PacienteId);
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        _logger.LogInformation(
            "Saving ML prediction for paciente: {PacienteId}, P(Pico)={PPico}, NivelRiesgo={NivelRiesgo}, CasoClinico={CasoClinico}",
            pacienteId, request.ProbabilidadPico, request.NivelRiesgo, request.CasoClinico);

        try
        {
            var prediccion = new PrediccionMl
            {
                PacienteId = pacienteId,
                ProbabilidadPico = Math.Clamp(request.ProbabilidadPico, 0.0, 1.0),
                NivelRiesgo = request.NivelRiesgo ?? "Normal",
                CasoClinico = request.CasoClinico,
                Imc = request.Imc,
                Z = request.Z,
                PPico = request.PPico,
                Recomendacion = request.Recomendacion ?? request.AccionAutomatizada,
                AccionAutomatizada = request.AccionAutomatizada,
                ModeloVersion = request.ModeloVersion ?? "pico-v1.0",
                FechaPrediccion = DateTime.UtcNow,
                FechaExpiracion = DateTime.UtcNow.AddHours(6)
            };

            await _db.PrediccionesMl.InsertOneAsync(prediccion);

            var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _auditoriaService.RegistrarAsync(usuarioId, "guardar_prediccion_ml", "predicciones_ml", prediccion.Id, ip);

            // Disparar notificación si es crítica
            _ = Task.Run(async () => await _notificacionService.NotificarPrediccionCriticaAsync(prediccion));

            return Ok(new { PrediccionId = prediccion.Id, message = "Predicción guardada correctamente" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving ML prediction for paciente {PacienteId}", pacienteId);
            return StatusCode(500, new { message = "Error al guardar predicción: " + ex.Message });
        }
    }

    /// <summary>
    /// GET /api/Sensores/predicciones/{pacienteId} [WEB + MÓVIL]
    /// MÓDULO 5: Historial de predicciones ML del paciente
    /// </summary>
    [HttpGet("predicciones/{pacienteId}")]
    public async Task<IActionResult> ObtenerPredicciones(string pacienteId, [FromQuery] int limite = 50)
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching predictions - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching {Limite} predictions for paciente: {PacienteId}", limite, pacienteId);

        try
        {
            var predicciones = await _db.PrediccionesMl
                .Find(p => p.PacienteId == pacienteId)
                .SortByDescending(p => p.FechaPrediccion)
                .Limit(limite)
                .ToListAsync();

            var response = predicciones.Select(p => new
            {
                p.Id,
                p.PacienteId,
                p.ProbabilidadPico,
                p.NivelRiesgo,
                p.CasoClinico,
                p.Imc,
                p.Z,
                p.PPico,
                p.Recomendacion,
                p.AccionAutomatizada,
                p.ModeloVersion,
                p.FechaPrediccion
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching predictions for paciente {PacienteId}", pacienteId);
            return StatusCode(500, new { message = "Error al obtener predicciones: " + ex.Message });
        }
    }

    /// <summary>
    /// GET /api/Sensores/predicciones/{pacienteId}/actual [WEB + MÓVIL]
    /// MÓDULO 5: Última predicción ML del paciente
    /// </summary>
    [HttpGet("predicciones/{pacienteId}/actual")]
    public async Task<IActionResult> ObtenerPrediccionActual(string pacienteId)
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching current prediction - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching current prediction for paciente: {PacienteId}", pacienteId);

        try
        {
            var prediccion = await _db.PrediccionesMl
                .Find(p => p.PacienteId == pacienteId)
                .SortByDescending(p => p.FechaPrediccion)
                .FirstOrDefaultAsync();

            if (prediccion == null)
            {
                return Ok(new { message = "Sin predicciones disponibles" });
            }

            var response = new
            {
                prediccion.Id,
                prediccion.PacienteId,
                prediccion.ProbabilidadPico,
                prediccion.NivelRiesgo,
                prediccion.CasoClinico,
                prediccion.Imc,
                prediccion.Z,
                prediccion.PPico,
                prediccion.Recomendacion,
                prediccion.AccionAutomatizada,
                prediccion.ModeloVersion,
                prediccion.FechaPrediccion
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching current prediction for paciente {PacienteId}", pacienteId);
            return StatusCode(500, new { message = "Error al obtener predicción: " + ex.Message });
        }
    }

    // ── Lecturas (Consulta) ───────────────────────────────────

    /// <summary>
    /// GET /api/Sensores/lecturas/{pacienteId} [WEB + MÓVIL]
    /// MÓDULO 5: Últimas N lecturas del paciente
    /// </summary>
    [HttpGet("lecturas/{pacienteId}")]
    public async Task<IActionResult> ObtenerLecturas(string pacienteId, [FromQuery] int limite = 100)
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
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
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
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
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
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
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
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
        var pacienteId = await ResolverPacienteIdAsync(request.PacienteId);
        if (string.IsNullOrEmpty(pacienteId)) return Unauthorized();

        var probabilidad = request.Probabilidad ?? request.ProbabilidadMl ?? 0.0;
        _logger.LogInformation("Creating metabolic event for paciente: {PacienteId}, risk: {NivelRiesgo}", pacienteId, request.NivelRiesgo);
        var evento = await _sensorService.CrearEventoAsync(
            pacienteId, probabilidad, request.NivelRiesgo, request.Descripcion);

        return Ok(new { EventoId = evento.Id, message = "Evento creado" });
    }

    /// <summary>
    /// GET /api/Sensores/eventos/{pacienteId} [WEB + MÓVIL]
    /// MÓDULO 5: Historial de eventos/alertas
    /// </summary>
    [HttpGet("eventos/{pacienteId}")]
    public async Task<IActionResult> ObtenerEventos(string pacienteId, [FromQuery] int limite = 50)
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
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
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
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
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed exporting PDF - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Exporting PDF for paciente: {PacienteId}", pacienteId);
        var lecturas = await _sensorService.ObtenerLecturasAsync(pacienteId, 1000);

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Timestamp,PulsoBpm,TemperaturaC,SudoracionGsr,ProbabilidadPico");
        foreach (var l in lecturas)
        {
            csv.AppendLine($"{l.Timestamp:O},{l.PulsoBpm},{l.TemperaturaC},{l.SudoracionGsr},{l.ProbabilidadPico}");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"lecturas_{pacienteId}_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // ── Tracking GPS ──────────────────────────────────────────

    /// <summary>
    /// POST /api/Sensores/tracking [MÓVIL]
    /// MÓDULO 5: Enviar ubicación GPS (emergencia o continua)
    /// </summary>
    [HttpPost("tracking")]
    public async Task<IActionResult> InsertarTracking([FromBody] TrackingGpsRequest request)
    {
        var pacienteId = await ResolverPacienteIdAsync(request.PacienteId);
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
    [RequestSizeLimit(10485760)]
    public async Task<IActionResult> InsertarTrackingBatch([FromBody] List<TrackingGpsRequest> request)
    {
        var pacienteId = await ResolverPacienteIdAsync(request.FirstOrDefault()?.PacienteId);
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
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching current tracking - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching current GPS location for paciente: {PacienteId}", pacienteId);
        var ubicacion = await _sensorService.ObtenerUltimaUbicacionAsync(pacienteId);
        if (ubicacion == null || ubicacion.Ubicacion == null || ubicacion.Ubicacion.Coordinates is not { Length: >= 2 })
        {
            _logger.LogWarning("No valid GPS location found for paciente: {PacienteId}", pacienteId);
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
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await _ownershipHelper.VerifyPacienteOwnershipAsync(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching GPS route - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching GPS route for paciente: {PacienteId} from {Desde} to {Hasta}", pacienteId, desde, hasta);
        var puntos = await _sensorService.ObtenerTrackingRangoAsync(pacienteId, desde, hasta);
        var response = puntos
            .Where(p => p.Ubicacion != null && p.Ubicacion.Coordinates is { Length: >= 2 })
            .Select(p => new TrackingResponse(
                p.Ubicacion!.Coordinates[0],
                p.Ubicacion.Coordinates[1],
                p.Timestamp,
                p.EsEmergencia));
        return Ok(response);
    }
}

public record CrearEventoRequest(
    double? Probabilidad = null,
    string NivelRiesgo = "",
    string Descripcion = "",
    double? ProbabilidadMl = null,
    string? PacienteId = null,
    string? DispositivoMac = null);

public record GuardarPrediccionRequest(
    [Required] string PacienteId,
    [Range(0.0, 1.0)] double ProbabilidadPico,
    string NivelRiesgo,
    string? CasoClinico = null,
    string? AccionAutomatizada = null,
    double? Imc = null,
    double? Z = null,
    [Range(0.0, 1.0)] double? PPico = null,
    string? Recomendacion = null,
    int? HorasEstimadas = null,
    string? ModeloVersion = null);

