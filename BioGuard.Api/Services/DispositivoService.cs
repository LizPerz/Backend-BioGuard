using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class DispositivoService
{
    private readonly IMongoDbContext _db;

    public DispositivoService(IMongoDbContext db) => _db = db;

    public async Task<Dispositivo?> VincularAsync(string pacienteId, string nombre, string macAddress)
    {
        var existente = await _db.FindFirstOrDefaultAsync(_db.Dispositivos, d => d.PacienteId == pacienteId);
        if (existente != null) return null;

        var dispositivo = new Dispositivo
        {
            PacienteId = pacienteId,
            NombreDispositivo = nombre,
            MacAddress = macAddress,
            Conectado = true,
            FechaVinculacion = DateTime.UtcNow
        };

        await _db.Dispositivos.InsertOneAsync(dispositivo);
        return dispositivo;
    }

    public async Task<bool> HeartbeatAsync(string pacienteId)
    {
        var update = Builders<Dispositivo>.Update.Set(d => d.Conectado, true);
        var result = await _db.Dispositivos.UpdateOneAsync(d => d.PacienteId == pacienteId, update);
        return result.ModifiedCount > 0;
    }

    public async Task<Dispositivo?> ObtenerPorPacienteAsync(string pacienteId)
    {
        return await _db.FindFirstOrDefaultAsync(_db.Dispositivos, d => d.PacienteId == pacienteId);
    }
}
