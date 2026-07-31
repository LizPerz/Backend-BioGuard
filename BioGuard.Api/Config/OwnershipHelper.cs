using MongoDB.Driver;
using BioGuard.Api.Models;

namespace BioGuard.Api.Config;

public class OwnershipHelper
{
    private readonly IMongoDbContext _db;

    public OwnershipHelper(IMongoDbContext db)
    {
        _db = db;
    }

    public async Task<bool> VerifyPacienteOwnershipAsync(string pacienteId, string userId, string role)
    {
        if (role == "paciente") return pacienteId == userId;
        if (role == "cuidador")
        {
            var cuidador = await _db.FindFirstOrDefaultAsync(_db.Cuidadores, c => c.Id == userId && c.PacienteId == pacienteId);
            return cuidador != null;
        }

        // dueno role — check patient is owned by this user
        var paciente = await _db.FindFirstOrDefaultAsync(_db.Pacientes, p => p.Id == pacienteId);
        if (paciente == null) return false;
        return paciente.UsuarioWebId == userId;
    }
}
