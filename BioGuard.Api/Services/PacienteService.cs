using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;
using Microsoft.Extensions.Logging;

namespace BioGuard.Api.Services;

public class PacienteService
{
    private readonly IMongoDbContext _db;
    private readonly ILogger<PacienteService> _logger;

    public PacienteService(IMongoDbContext db, ILogger<PacienteService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Paciente?> GetByCodigoAsync(string codigo)
    {
        return await _db.FindFirstOrDefaultAsync(_db.Pacientes, p => p.CodigoAccesoQr == codigo);
    }

    public async Task<Paciente?> GetByIdAsync(string id)
    {
        return await _db.FindFirstOrDefaultAsync(_db.Pacientes, p => p.Id == id);
    }

    public async Task<List<Paciente>> GetAllByUsuarioAsync(string usuarioWebId)
    {
        return await _db.FindToListAsync(_db.Pacientes, p => p.UsuarioWebId == usuarioWebId);
    }

    public async Task UpdateBiometriaAsync(string pacienteId, UpdateBiometriaRequest request)
    {
        var update = Builders<Paciente>.Update
            .Set(p => p.Biometria.Edad, request.Edad)
            .Set(p => p.Biometria.PesoKg, request.PesoKg)
            .Set(p => p.Biometria.EstaturaCm, request.EstaturaCm)
            .Set(p => p.Biometria.EsDiabetico, request.EsDiabetico)
            .Set(p => p.Biometria.FamiliaresDiabetes, request.FamiliaresDiabetes)
            .Set(p => p.Biometria.ActividadFisica, request.ActividadFisica)
            .Set(p => p.PerfilCompletado, true);

        await _db.Pacientes.UpdateOneAsync(p => p.Id == pacienteId, update);
        _logger.LogInformation("Biometrics updated for patient: {PacienteId}", pacienteId);
    }

    public async Task<string> CrearPacienteAsync(string usuarioWebId, string nombre)
    {
        var codigo = GenerarCodigo();
        var paciente = new Paciente
        {
            UsuarioWebId = usuarioWebId,
            CodigoAccesoQr = codigo,
            Nombre = nombre,
            FechaRegistro = DateTime.UtcNow
        };
        await _db.Pacientes.InsertOneAsync(paciente);
        _logger.LogInformation("Patient created: {PacienteId} for user: {UsuarioWebId}", paciente.Id, usuarioWebId);
        return codigo;
    }

    public async Task<bool> UpdateNombreAsync(string pacienteId, string nombre)
    {
        var update = Builders<Paciente>.Update.Set(p => p.Nombre, nombre);
        var result = await _db.Pacientes.UpdateOneAsync(p => p.Id == pacienteId, update);
        if (result.ModifiedCount == 0)
        {
            _logger.LogWarning("Patient name update not found or unchanged: {PacienteId}", pacienteId);
        }
        else
        {
            _logger.LogInformation("Patient name updated: {PacienteId}", pacienteId);
        }
        return result.ModifiedCount > 0;
    }

    public async Task<bool> EliminarAsync(string pacienteId)
    {
        await _db.DeleteManyAsync(_db.LecturasSensores, l => l.Meta.PacienteId == pacienteId);
        await _db.DeleteManyAsync(_db.EventosMetabolicos, e => e.PacienteId == pacienteId);
        await _db.DeleteManyAsync(_db.TrackingGps, t => t.Meta.PacienteId == pacienteId);
        await _db.DeleteManyAsync(_db.Notificaciones, n => n.PacienteId == pacienteId);
        await _db.DeleteManyAsync(_db.Dispositivos, d => d.PacienteId == pacienteId);
        await _db.DeleteManyAsync(_db.Medicamentos, m => m.PacienteId == pacienteId);
        await _db.DeleteManyAsync(_db.Alertas, a => a.PacienteId == pacienteId);

        var result = await _db.Pacientes.DeleteOneAsync(p => p.Id == pacienteId);
        if (result.DeletedCount == 0)
        {
            _logger.LogWarning("Patient delete not found: {PacienteId}", pacienteId);
        }
        else
        {
            _logger.LogInformation("Patient deleted: {PacienteId}", pacienteId);
        }
        return result.DeletedCount > 0;
    }

    private static string GenerarCodigo()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 8) /*Aqui cambiar a 8*/
            .Select(s => s[System.Security.Cryptography.RandomNumberGenerator.GetInt32(s.Length)]).ToArray());
    }
}
