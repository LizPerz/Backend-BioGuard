using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class MLService
{
    private readonly IMongoDbContext _db;
    private readonly ILogger<MLService> _logger;

    public MLService(IMongoDbContext db, ILogger<MLService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<PrediccionMl>> ObtenerPrediccionesAsync(string pacienteId)
    {
        _logger.LogInformation("Obteniendo reportes de pico glucémico para paciente {PacienteId}", pacienteId);
        var filter = Builders<PrediccionMl>.Filter.Eq(p => p.PacienteId, pacienteId);
        var sort = Builders<PrediccionMl>.Sort.Descending(p => p.FechaPrediccion);
        return await _db.FindToListAsync(_db.PrediccionesMl, filter, sort, 20);
    }

    public async Task<PrediccionMl?> ObtenerPrediccionActualAsync(string pacienteId)
    {
        _logger.LogInformation("Obteniendo reporte actual para paciente {PacienteId}", pacienteId);
        var filter = Builders<PrediccionMl>.Filter.And(
            Builders<PrediccionMl>.Filter.Eq(p => p.PacienteId, pacienteId),
            Builders<PrediccionMl>.Filter.Gt(p => p.FechaExpiracion, DateTime.UtcNow));
        var sort = Builders<PrediccionMl>.Sort.Descending(p => p.FechaPrediccion);
        return await _db.FindFirstOrDefaultAsync(_db.PrediccionesMl, filter, sort);
    }

    public async Task<List<string>> ObtenerRecomendacionesAsync(string pacienteId)
    {
        _logger.LogInformation("Obteniendo recomendaciones para paciente {PacienteId}", pacienteId);
        var prediccion = await ObtenerPrediccionActualAsync(pacienteId);
        if (prediccion == null)
        {
            _logger.LogWarning("No hay reporte activo para paciente {PacienteId}", pacienteId);
            return new List<string>();
        }

        var recomendaciones = new List<string> { prediccion.Recomendacion };

        if (prediccion.NivelRiesgo == "Critico")
        {
            recomendaciones.Add("Contactar al cuidador de inmediato.");
            recomendaciones.Add("Verificar niveles de glucosa si es posible.");
        }
        else if (prediccion.NivelRiesgo == "Pre-Pico")
        {
            recomendaciones.Add("Mantener hidratación constante.");
            recomendaciones.Add("Evitar actividad física intensa.");
        }

        return recomendaciones;
    }

    public async Task<PrediccionMl> GuardarPrediccionAsync(PrediccionMl entidad)
    {
        if (entidad.FechaPrediccion == default)
            entidad.FechaPrediccion = DateTime.UtcNow;

        _logger.LogInformation("Guardando reporte de pico glucémico para paciente {PacienteId}", entidad.PacienteId);
        await _db.PrediccionesMl.InsertOneAsync(entidad);
        _logger.LogInformation("Reporte guardado con ID {PrediccionId}", entidad.Id);
        return entidad;
    }
}
