using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class MLService
{
    private readonly IMongoDbContext _db;

    public MLService(IMongoDbContext db) => _db = db;

    public async Task<List<PrediccionMl>> ObtenerPrediccionesAsync(string pacienteId)
    {
        var filter = Builders<PrediccionMl>.Filter.Eq(p => p.PacienteId, pacienteId);
        var sort = Builders<PrediccionMl>.Sort.Descending(p => p.FechaPrediccion);
        return await _db.FindToListAsync(_db.PrediccionesMl, filter, sort, 20);
    }

    public async Task<PrediccionMl?> ObtenerPrediccionActualAsync(string pacienteId)
    {
        var filter = Builders<PrediccionMl>.Filter.And(
            Builders<PrediccionMl>.Filter.Eq(p => p.PacienteId, pacienteId),
            Builders<PrediccionMl>.Filter.Gt(p => p.FechaExpiracion, DateTime.UtcNow));
        var sort = Builders<PrediccionMl>.Sort.Descending(p => p.FechaPrediccion);
        return await _db.FindFirstOrDefaultAsync(_db.PrediccionesMl, filter, sort);
    }

    public async Task<List<string>> ObtenerRecomendacionesAsync(string pacienteId)
    {
        var prediccion = await ObtenerPrediccionActualAsync(pacienteId);
        if (prediccion == null) return new List<string>();

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

    public async Task<List<ModeloMl>> ObtenerModelosAsync()
    {
        var filter = Builders<ModeloMl>.Filter.Empty;
        var sort = Builders<ModeloMl>.Sort.Descending(m => m.FechaEntrenamiento);
        return await _db.FindToListAsync(_db.ModelosMl, filter, sort);
    }

    public async Task<ModeloMl?> ObtenerModeloActivoAsync()
    {
        return await _db.FindFirstOrDefaultAsync(_db.ModelosMl, m => m.Activo);
    }

    public async Task<ModeloMl> CrearModeloAsync(ModeloMl modelo)
    {
        await _db.ModelosMl.InsertOneAsync(modelo);
        return modelo;
    }

    public async Task<ModeloMl?> ObtenerMetricasAsync(string modeloId)
    {
        return await _db.FindFirstOrDefaultAsync(_db.ModelosMl, m => m.Id == modeloId);
    }
}
