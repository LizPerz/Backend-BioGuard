using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 6: AI Console (Exclusivo Plan PRO)
/// ENDPOINT WEB
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MLController : ControllerBase
{
    private readonly MLService _mlService;

    public MLController(MLService mlService)
    {
        _mlService = mlService;
    }

    // ── Predicciones ──────────────────────────────────────────

    /// <summary>
    /// GET /api/ML/predicciones/{pacienteId} [WEB]
    /// MÓDULO 6: Historial de predicciones AI
    /// </summary>
    [HttpGet("predicciones/{pacienteId}")]
    public async Task<IActionResult> ObtenerPredicciones(string pacienteId)
    {
        var predicciones = await _mlService.ObtenerPrediccionesAsync(pacienteId);
        var response = predicciones.Select(p => new
        {
            p.Id,
            Probabilidad = p.ProbabilidadPico,
            p.NivelRiesgo,
            p.Recomendacion,
            p.FechaPrediccion,
            p.HorasEstimadas,
            p.ModeloVersion
        });
        return Ok(response);
    }

    /// <summary>
    /// GET /api/ML/predicciones/{pacienteId}/actual [WEB]
    /// MÓDULO 6: Predicción actual ("próximas 2 horas")
    /// </summary>
    [HttpGet("predicciones/{pacienteId}/actual")]
    public async Task<IActionResult> PrediccionActual(string pacienteId)
    {
        var prediccion = await _mlService.ObtenerPrediccionActualAsync(pacienteId);
        if (prediccion == null) return Ok(new { message = "Sin predicción activa" });
        return Ok(new
        {
            prediccion.Id,
            Probabilidad = prediccion.ProbabilidadPico,
            prediccion.NivelRiesgo,
            prediccion.Recomendacion,
            prediccion.FechaPrediccion,
            prediccion.HorasEstimadas
        });
    }

    // ── Recomendaciones ───────────────────────────────────────

    /// <summary>
    /// GET /api/ML/recomendaciones/{pacienteId} [WEB]
    /// MÓDULO 6: Sugerencias clínicas basadas en patrones
    /// </summary>
    [HttpGet("recomendaciones/{pacienteId}")]
    public async Task<IActionResult> Recomendaciones(string pacienteId)
    {
        var recomendaciones = await _mlService.ObtenerRecomendacionesAsync(pacienteId);
        return Ok(new { Recomendaciones = recomendaciones });
    }

    // ── Gestión de modelos ────────────────────────────────────

    /// <summary>
    /// GET /api/ML/modelos [WEB]
    /// MÓDULO 6: Versiones del modelo entrenado
    /// </summary>
    [HttpGet("modelos")]
    public async Task<IActionResult> ListarModelos()
    {
        var modelos = await _mlService.ObtenerModelosAsync();
        var response = modelos.Select(m => new
        {
            m.Id,
            m.Version,
            m.Accuracy,
            m.Precision,
            m.Recall,
            m.F1Score,
            m.Activo,
            m.TotalMuestras,
            m.FechaEntrenamiento,
            m.Descripcion
        });
        return Ok(response);
    }

    /// <summary>
    /// POST /api/ML/entrenar [WEB]
    /// MÓDULO 6: Disparar re-entrenamiento del modelo
    /// </summary>
    [HttpPost("entrenar")]
    public async Task<IActionResult> EntrenarModelo([FromBody] EntrenarModeloRequest request)
    {
        var modelo = new ModeloMl
        {
            Version = request.Version,
            Accuracy = 0.0,
            Activo = false,
            FechaEntrenamiento = DateTime.UtcNow,
            Descripcion = request.Descripcion
        };

        var result = await _mlService.CrearModeloAsync(modelo);
        return Ok(new { ModeloId = result.Id, message = "Entrenamiento iniciado" });
    }

    /// <summary>
    /// GET /api/ML/metricas/{modeloId} [WEB]
    /// MÓDULO 6: Accuracy, precision, recall, F1
    /// </summary>
    [HttpGet("metricas/{modeloId}")]
    public async Task<IActionResult> MetricasModelo(string modeloId)
    {
        var modelo = await _mlService.ObtenerMetricasAsync(modeloId);
        if (modelo == null) return NotFound();

        return Ok(new
        {
            modelo.Version,
            modelo.Accuracy,
            modelo.Precision,
            modelo.Recall,
            F1 = modelo.F1Score,
            modelo.TotalMuestras,
            modelo.FechaEntrenamiento
        });
    }
}

public record EntrenarModeloRequest(string Version, string Descripcion);
