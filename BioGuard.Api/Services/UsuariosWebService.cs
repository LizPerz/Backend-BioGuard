using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class UsuariosWebService
{
    private readonly IMongoDbContext _db;

    public UsuariosWebService(IMongoDbContext db) => _db = db;

    public async Task<UsuarioWeb?> GetByIdAsync(string id)
    {
        return await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Id == id);
    }

    public async Task<Plan?> GetPlanAsync(string usuarioId)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Id == usuarioId);
        if (user == null) return null;
        return await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Id == user.PlanId);
    }

    public async Task<bool> UpdatePerfilAsync(string usuarioId, UpdatePerfilRequest request)
    {
        var update = Builders<UsuarioWeb>.Update
            .Set(u => u.Nombre, request.Nombre)
            .Set(u => u.ApellidoPaterno, request.ApellidoPaterno)
            .Set(u => u.ApellidoMaterno, request.ApellidoMaterno);

        var result = await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == usuarioId, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> CambiarCorreoAsync(string usuarioId, string nuevoCorreo)
    {
        var exists = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == nuevoCorreo);
        if (exists != null) return false;

        var update = Builders<UsuarioWeb>.Update.Set(u => u.Correo, nuevoCorreo);
        var result = await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == usuarioId, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> SubirFotoAsync(string usuarioId, string fotoBase64)
    {
        var update = Builders<UsuarioWeb>.Update.Set(u => u.FotoPerfil, fotoBase64);
        var result = await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == usuarioId, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> CambiarPlanAsync(string usuarioId, string planNombre)
    {
        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Nombre == planNombre);
        if (plan == null) return false;

        var update = Builders<UsuarioWeb>.Update.Set(u => u.PlanId, plan.Id);
        var result = await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == usuarioId, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> EliminarCuentaAsync(string usuarioId)
    {
        await _db.DeleteManyAsync(_db.Cuidadores, c => c.UsuarioWebId == usuarioId);

        var pacientes = await _db.FindToListAsync(_db.Pacientes, p => p.UsuarioWebId == usuarioId);
        foreach (var paciente in pacientes)
        {
            await _db.DeleteManyAsync(_db.LecturasSensores, l => l.Meta.PacienteId == paciente.Id);
            await _db.DeleteManyAsync(_db.EventosMetabolicos, e => e.PacienteId == paciente.Id);
            await _db.DeleteManyAsync(_db.TrackingGps, t => t.Meta.PacienteId == paciente.Id);
            await _db.DeleteManyAsync(_db.Notificaciones, n => n.PacienteId == paciente.Id);
            await _db.DeleteManyAsync(_db.Dispositivos, d => d.PacienteId == paciente.Id);
            await _db.DeleteManyAsync(_db.Medicamentos, m => m.PacienteId == paciente.Id);
            await _db.DeleteManyAsync(_db.Alertas, a => a.PacienteId == paciente.Id);
        }
        await _db.DeleteManyAsync(_db.Pacientes, p => p.UsuarioWebId == usuarioId);
        await _db.DeleteManyAsync(_db.Pagos, p => p.UsuarioWebId == usuarioId);

        var result = await _db.UsuariosWeb.DeleteOneAsync(u => u.Id == usuarioId);
        return result.DeletedCount > 0;
    }

    public async Task<UsuarioWeb?> GetByEmailAsync(string correo)
    {
        return await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == correo);
    }
}
