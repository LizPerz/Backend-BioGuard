using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using Microsoft.Extensions.Logging;

namespace BioGuard.Api.Services;

public class CuidadorService
{
    private readonly IMongoDbContext _db;
    private readonly ILogger<CuidadorService> _logger;

    public CuidadorService(IMongoDbContext db, ILogger<CuidadorService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<Cuidador>> ObtenerPorUsuarioAsync(string usuarioId)
    {
        return await _db.FindToListAsync(_db.Cuidadores, c => c.UsuarioWebId == usuarioId);
    }

    public async Task<Cuidador?> ObtenerPorIdAsync(string id)
    {
        return await _db.FindFirstOrDefaultAsync(_db.Cuidadores, c => c.Id == id);
    }

    public async Task<List<Cuidador>> ObtenerPorPacienteAsync(string pacienteId)
    {
        return await _db.FindToListAsync(_db.Cuidadores, c => c.PacienteId == pacienteId);
    }

    public async Task<int> ContarPorPacienteAsync(string pacienteId)
    {
        return (int)await _db.CountDocumentsAsync(_db.Cuidadores, c => c.PacienteId == pacienteId);
    }

    public async Task<(Cuidador? cuidador, string codigo)> CrearAsync(
        string usuarioId, string pacienteId, string nombre, string parentesco,
        string telefono, string correo)
    {
        var codigo = GenerarCodigo();
        var cuidador = new Cuidador
        {
            UsuarioWebId = usuarioId,
            PacienteId = pacienteId,
            CodigoAccesoQr = codigo,
            Nombre = nombre,
            Parentesco = parentesco,
            Telefono = telefono,
            Correo = correo,
            FechaAutorizacion = DateTime.UtcNow
        };

        await _db.Cuidadores.InsertOneAsync(cuidador);
        _logger.LogInformation("Caregiver created: {CuidadorId} for patient: {PacienteId}", cuidador.Id, pacienteId);
        return (cuidador, codigo);
    }

    public async Task<bool> ActualizarAsync(string id, string nombre, string parentesco)
    {
        var update = Builders<Cuidador>.Update
            .Set(c => c.Nombre, nombre)
            .Set(c => c.Parentesco, parentesco);

        var result = await _db.Cuidadores.UpdateOneAsync(c => c.Id == id, update);
        if (result.ModifiedCount == 0)
        {
            _logger.LogWarning("Caregiver update not found or unchanged: {CuidadorId}", id);
        }
        else
        {
            _logger.LogInformation("Caregiver updated: {CuidadorId}", id);
        }
        return result.ModifiedCount > 0;
    }

    public async Task<bool> EliminarAsync(string id)
    {
        var result = await _db.Cuidadores.DeleteOneAsync(c => c.Id == id);
        if (result.DeletedCount == 0)
        {
            _logger.LogWarning("Caregiver delete not found: {CuidadorId}", id);
        }
        else
        {
            _logger.LogInformation("Caregiver deleted: {CuidadorId}", id);
        }
        return result.DeletedCount > 0;
    }

    public async Task<string> RegenerarQRAsync(string id)
    {
        var codigo = GenerarCodigo();
        var update = Builders<Cuidador>.Update.Set(c => c.CodigoAccesoQr, codigo);
        await _db.Cuidadores.UpdateOneAsync(c => c.Id == id, update);
        _logger.LogInformation("QR regenerated for caregiver: {CuidadorId}", id);
        return codigo;
    }

    private static string GenerarCodigo()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return "CU-" + new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[System.Security.Cryptography.RandomNumberGenerator.GetInt32(s.Length)]).ToArray());
    }
}
