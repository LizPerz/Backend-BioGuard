using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using Microsoft.Extensions.Logging;

namespace BioGuard.Api.Services;

public class MedicamentoService
{
    private readonly IMongoDbContext _db;
    private readonly ILogger<MedicamentoService> _logger;

    public MedicamentoService(IMongoDbContext db, ILogger<MedicamentoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<Medicamento>> ObtenerPorPacienteAsync(string pacienteId)
    {
        var filter = Builders<Medicamento>.Filter.Eq(m => m.PacienteId, pacienteId);
        var sort = Builders<Medicamento>.Sort.Descending(m => m.FechaCreacion);
        return await _db.FindToListAsync(_db.Medicamentos, filter, sort);
    }

    public async Task<Medicamento?> ObtenerPorIdAsync(string id)
    {
        return await _db.FindFirstOrDefaultAsync(_db.Medicamentos, m => m.Id == id);
    }

    public async Task<Medicamento> CrearAsync(string pacienteId, string nombre,
        string dosis, string horario, string? notas = null)
    {
        var medicamento = new Medicamento
        {
            PacienteId = pacienteId,
            Nombre = nombre,
            Dosis = dosis,
            Horario = horario,
            Notas = notas,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        await _db.Medicamentos.InsertOneAsync(medicamento);
        _logger.LogInformation("Medication created for patient: {PacienteId}", pacienteId);
        return medicamento;
    }

    public async Task<bool> ActualizarAsync(string id, string nombre,
        string dosis, string horario, string? notas)
    {
        var update = Builders<Medicamento>.Update
            .Set(m => m.Nombre, nombre)
            .Set(m => m.Dosis, dosis)
            .Set(m => m.Horario, horario)
            .Set(m => m.Notas, notas);

        var result = await _db.Medicamentos.UpdateOneAsync(m => m.Id == id, update);
        if (result.ModifiedCount == 0)
        {
            _logger.LogWarning("Medication update not found or unchanged: {MedicamentoId}", id);
        }
        else
        {
            _logger.LogInformation("Medication updated: {MedicamentoId}", id);
        }
        return result.ModifiedCount > 0;
    }

    public async Task<bool> RegistrarTomaAsync(string medicamentoId)
    {
        var update = Builders<Medicamento>.Update.Set(m => m.UltimaToma, DateTime.UtcNow);
        var result = await _db.Medicamentos.UpdateOneAsync(m => m.Id == medicamentoId, update);
        _logger.LogInformation("Medication dose recorded: {MedicamentoId}", medicamentoId);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> ActivarAsync(string id, bool activo)
    {
        var update = Builders<Medicamento>.Update.Set(m => m.Activo, activo);
        var result = await _db.Medicamentos.UpdateOneAsync(m => m.Id == id, update);
        _logger.LogInformation("Medication {MedicamentoId} activated: {Activo}", id, activo);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> EliminarAsync(string id)
    {
        var result = await _db.Medicamentos.DeleteOneAsync(m => m.Id == id);
        if (result.DeletedCount == 0)
        {
            _logger.LogWarning("Medication delete not found: {MedicamentoId}", id);
        }
        else
        {
            _logger.LogInformation("Medication deleted: {MedicamentoId}", id);
        }
        return result.DeletedCount > 0;
    }

    public async Task<bool> EliminarPorPacienteAsync(string pacienteId)
    {
        var result = await _db.DeleteManyAsync(_db.Medicamentos, m => m.PacienteId == pacienteId);
        _logger.LogInformation("Medications deleted for patient: {PacienteId}, count: {Count}", pacienteId, result.DeletedCount);
        return result.DeletedCount > 0;
    }
}
