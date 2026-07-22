using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class DispositivoService
{
    private readonly IMongoDbContext _db;
    private readonly ILogger<DispositivoService> _logger;

    public DispositivoService(IMongoDbContext db, ILogger<DispositivoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Dispositivo?> VincularAsync(string pacienteId, string nombre, string macAddress)
    {
        _logger.LogInformation("Vinculando dispositivo para paciente {PacienteId}", pacienteId);
        var existente = await _db.FindFirstOrDefaultAsync(_db.Dispositivos, d => d.PacienteId == pacienteId);
        if (existente != null)
        {
            _logger.LogWarning("Paciente {PacienteId} ya tiene un dispositivo vinculado", pacienteId);
            return null;
        }

        var dispositivo = new Dispositivo
        {
            PacienteId = pacienteId,
            NombreDispositivo = nombre,
            MacAddress = macAddress,
            Conectado = true,
            FechaVinculacion = DateTime.UtcNow
        };

        await _db.Dispositivos.InsertOneAsync(dispositivo);
        _logger.LogInformation("Dispositivo vinculado con ID {DispositivoId} para paciente {PacienteId}", dispositivo.Id, pacienteId);
        return dispositivo;
    }

    public async Task<bool> HeartbeatAsync(string pacienteId)
    {
        _logger.LogInformation("Heartbeat recibido para paciente {PacienteId}", pacienteId);
        var update = Builders<Dispositivo>.Update.Set(d => d.Conectado, true);
        var result = await _db.Dispositivos.UpdateOneAsync(d => d.PacienteId == pacienteId, update);
        if (result.ModifiedCount == 0)
            _logger.LogWarning("No se encontró dispositivo para paciente {PacienteId}", pacienteId);
        return result.ModifiedCount > 0;
    }

    public async Task<Dispositivo?> ObtenerPorPacienteAsync(string pacienteId)
    {
        _logger.LogInformation("Buscando dispositivo para paciente {PacienteId}", pacienteId);
        return await _db.FindFirstOrDefaultAsync(_db.Dispositivos, d => d.PacienteId == pacienteId);
    }

    public async Task<bool> ActualizarAsync(string id, string nombre)
    {
        _logger.LogInformation("Actualizando dispositivo {DispositivoId}", id);
        var update = Builders<Dispositivo>.Update.Set(d => d.NombreDispositivo, nombre);
        var result = await _db.Dispositivos.UpdateOneAsync(d => d.Id == id, update);
        if (result.ModifiedCount == 0)
            _logger.LogWarning("Dispositivo no encontrado para actualizar: {DispositivoId}", id);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> EliminarAsync(string id)
    {
        _logger.LogInformation("Eliminando dispositivo {DispositivoId}", id);
        var result = await _db.Dispositivos.DeleteOneAsync(d => d.Id == id);
        if (result.DeletedCount == 0)
            _logger.LogWarning("Dispositivo no encontrado para eliminar: {DispositivoId}", id);
        return result.DeletedCount > 0;
    }
}
