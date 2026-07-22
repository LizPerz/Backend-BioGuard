using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using Microsoft.Extensions.Logging;

namespace BioGuard.Api.Services;

public class AlertaService
{
    private readonly IMongoDbContext _db;
    private readonly ILogger<AlertaService> _logger;

    public AlertaService(IMongoDbContext db, ILogger<AlertaService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<Alerta>> ObtenerPorPacienteAsync(string pacienteId, int limite = 50)
    {
        var filter = Builders<Alerta>.Filter.Eq(a => a.PacienteId, pacienteId);
        var sort = Builders<Alerta>.Sort.Descending(a => a.FechaCreacion);
        return await _db.FindToListAsync(_db.Alertas, filter, sort, limite);
    }

    public async Task<List<Alerta>> ObtenerPendientesAsync(string pacienteId)
    {
        var filter = Builders<Alerta>.Filter.And(
            Builders<Alerta>.Filter.Eq(a => a.PacienteId, pacienteId),
            Builders<Alerta>.Filter.Eq(a => a.Atendida, false));
        var sort = Builders<Alerta>.Sort.Descending(a => a.FechaCreacion);
        return await _db.FindToListAsync(_db.Alertas, filter, sort);
    }

    public async Task<Alerta?> ObtenerPorIdAsync(string id)
    {
        return await _db.FindFirstOrDefaultAsync(_db.Alertas, a => a.Id == id);
    }

    public async Task<Alerta> CrearAsync(string pacienteId, string tipo, string nivel,
        string titulo, string mensaje, SensorData? sensorData = null)
    {
        var alerta = new Alerta
        {
            PacienteId = pacienteId,
            Tipo = tipo,
            Nivel = nivel,
            Titulo = titulo,
            Mensaje = mensaje,
            SensorData = sensorData,
            Atendida = false,
            FechaCreacion = DateTime.UtcNow
        };

        await _db.Alertas.InsertOneAsync(alerta);
        _logger.LogInformation("Alert created for patient: {PacienteId}, type: {Tipo}, level: {Nivel}", pacienteId, tipo, nivel);
        return alerta;
    }

    public async Task<bool> ResolverAsync(string alertaId, string cuidadorId, string? accionTomada = null)
    {
        var update = Builders<Alerta>.Update
            .Set(a => a.Atendida, true)
            .Set(a => a.AtendidaPorId, cuidadorId)
            .Set(a => a.FechaAtencion, DateTime.UtcNow);

        var result = await _db.Alertas.UpdateOneAsync(a => a.Id == alertaId, update);
        if (result.ModifiedCount == 0)
        {
            _logger.LogWarning("Alert resolve not found or already resolved: {AlertaId}", alertaId);
        }
        else
        {
            _logger.LogInformation("Alert resolved: {AlertaId} by caregiver: {CuidadorId}", alertaId, cuidadorId);
        }
        return result.ModifiedCount > 0;
    }

    public async Task<bool> EliminarAsync(string id)
    {
        var result = await _db.Alertas.DeleteOneAsync(a => a.Id == id);
        if (result.DeletedCount == 0)
        {
            _logger.LogWarning("Alert delete not found: {AlertaId}", id);
        }
        else
        {
            _logger.LogInformation("Alert deleted: {AlertaId}", id);
        }
        return result.DeletedCount > 0;
    }

    public async Task<bool> EliminarPorPacienteAsync(string pacienteId)
    {
        var result = await _db.DeleteManyAsync(_db.Alertas, a => a.PacienteId == pacienteId);
        _logger.LogInformation("Alerts deleted for patient: {PacienteId}, count: {Count}", pacienteId, result.DeletedCount);
        return result.DeletedCount > 0;
    }
}
