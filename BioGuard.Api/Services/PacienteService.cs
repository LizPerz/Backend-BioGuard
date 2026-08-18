using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;
using Microsoft.Extensions.Logging;

namespace BioGuard.Api.Services;

public class PacienteService
{
    public const int CodigoVigenciaMinutos = 5;

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
            .Set(p => p.Biometria, new Biometria
            {
                Edad = request.Edad,
                PesoKg = request.PesoKg,
                EstaturaCm = request.EstaturaCm,
                EsDiabetico = request.EsDiabetico,
                FamiliaresDiabetes = request.FamiliaresDiabetes,
                ActividadFisica = request.ActividadFisica,
                Sexo = request.Sexo ?? string.Empty
            })
            .Set(p => p.PerfilCompletado, true);

        if (request.FechaNacimiento.HasValue)
        {
            update = update.Set(p => p.FechaNacimiento, request.FechaNacimiento);
        }

        await _db.Pacientes.UpdateOneAsync(p => p.Id == pacienteId, update);
        _logger.LogInformation("Biometrics updated for patient: {PacienteId}", pacienteId);
    }

    public async Task<string> CrearPacienteAsync(string usuarioWebId, CrearPacienteRequest request)
    {
        var codigo = GenerarCodigo();
        var tieneDatosBiometricos = request.FechaNacimiento.HasValue || request.Edad > 0 ||
            request.PesoKg > 0 || request.EstaturaCm > 0 ||
            !string.IsNullOrWhiteSpace(request.Sexo) || request.EsDiabetico ||
            request.FamiliaresDiabetes || !string.IsNullOrWhiteSpace(request.ActividadFisica);

        var paciente = new Paciente
        {
            UsuarioWebId = usuarioWebId,
            CodigoAccesoQr = codigo,
            CodigoExpira = DateTime.UtcNow.AddMinutes(CodigoVigenciaMinutos),
            Nombre = request.Nombre,
            FechaNacimiento = request.FechaNacimiento,
            FechaRegistro = DateTime.UtcNow,
            PerfilCompletado = tieneDatosBiometricos,
            Biometria = new Biometria
            {
                Edad = request.Edad,
                PesoKg = request.PesoKg,
                EstaturaCm = request.EstaturaCm,
                EsDiabetico = request.EsDiabetico,
                FamiliaresDiabetes = request.FamiliaresDiabetes,
                ActividadFisica = request.ActividadFisica ?? string.Empty,
                Sexo = request.Sexo ?? string.Empty
            }
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
        await _db.DeleteManyAsync(_db.Cuidadores, c => c.PacienteId == pacienteId);

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

    public async Task<(string codigo, DateTime expira, bool qrUsado)> ObtenerOCrearCodigoAsync(string pacienteId)
    {
        var paciente = await GetByIdAsync(pacienteId);
        if (paciente == null) return (string.Empty, DateTime.UtcNow, false);
        var expirado = !paciente.CodigoExpira.HasValue || paciente.CodigoExpira.Value < DateTime.UtcNow;
        // Regenerar si el código está vacío, expirado, o ya fue usado.
        // Esto asegura que después de un logout (QrUsado=true) el web muestre
        // un QR nuevo listo para el siguiente login.
        if (string.IsNullOrWhiteSpace(paciente.CodigoAccesoQr) || expirado || paciente.QrUsado)
        {
            var codigo = await RegenerarQRAsync(pacienteId);
            return (codigo, DateTime.UtcNow.AddMinutes(CodigoVigenciaMinutos), false);
        }
        return (paciente.CodigoAccesoQr, paciente.CodigoExpira!.Value, paciente.QrUsado);
    }

    public async Task<string> RegenerarQRAsync(string pacienteId)
    {
        var codigo = GenerarCodigo();
        var update = Builders<Paciente>.Update
            .Set(p => p.CodigoAccesoQr, codigo)
            .Set(p => p.CodigoExpira, DateTime.UtcNow.AddMinutes(CodigoVigenciaMinutos))
            .Set(p => p.QrUsado, false);
        await _db.Pacientes.UpdateOneAsync(p => p.Id == pacienteId, update);
        _logger.LogInformation("QR regenerated for patient: {PacienteId}", pacienteId);
        return codigo;
    }

    public async Task<bool> SubirFotoAsync(string pacienteId, string fotoBase64)
    {
        var update = Builders<Paciente>.Update.Set(p => p.Foto, fotoBase64);
        var result = await _db.Pacientes.UpdateOneAsync(p => p.Id == pacienteId, update);
        if (result.MatchedCount == 0)
        {
            _logger.LogWarning("Photo upload not found patient: {PacienteId}", pacienteId);
            return false;
        }
        _logger.LogInformation("Photo updated for patient: {PacienteId}", pacienteId);
        return true;
    }

    public async Task<bool> EliminarFotoAsync(string pacienteId)
    {
        var update = Builders<Paciente>.Update.Set(p => p.Foto, null);
        var result = await _db.Pacientes.UpdateOneAsync(p => p.Id == pacienteId, update);
        if (result.MatchedCount == 0)
        {
            _logger.LogWarning("Photo delete not found patient: {PacienteId}", pacienteId);
            return false;
        }
        _logger.LogInformation("Photo deleted for patient: {PacienteId}", pacienteId);
        return true;
    }

    private static string GenerarCodigo()
    {
        var numeros = new char[8];
        for (int i = 0; i < numeros.Length; i++)
            numeros[i] = (char)System.Security.Cryptography.RandomNumberGenerator.GetInt32('0', '9' + 1);
        return new string(numeros);
    }
}
