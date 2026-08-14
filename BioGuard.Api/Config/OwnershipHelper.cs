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
            var cuidador = await _db.FindFirstOrDefaultAsync(_db.Cuidadores, c =>
                (c.Id == userId || c.UsuarioWebId == userId || c.UsuarioVinculadoId == userId) && c.PacienteId == pacienteId);
            return cuidador != null;
        }

        var paciente = await _db.FindFirstOrDefaultAsync(_db.Pacientes, p => p.Id == pacienteId);
        return paciente?.UsuarioWebId == userId;
    }
}
