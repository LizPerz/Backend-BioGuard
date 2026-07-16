using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class NotificacionService
{
    private readonly IMongoDbContext _db;

    public NotificacionService(IMongoDbContext db) => _db = db;

    public async Task<List<Notificacion>> ObtenerPorPacienteAsync(string pacienteId)
    {
        var filter = Builders<Notificacion>.Filter.Eq(n => n.PacienteId, pacienteId);
        var sort = Builders<Notificacion>.Sort.Descending(n => n.FechaEnvio);
        return await _db.FindToListAsync(_db.Notificaciones, filter, sort, 50);
    }

    public async Task<bool> MarcarLeidaAsync(string notificacionId)
    {
        var update = Builders<Notificacion>.Update.Set(n => n.Leida, true);
        var result = await _db.Notificaciones.UpdateOneAsync(n => n.Id == notificacionId, update);
        return result.ModifiedCount > 0;
    }

    public async Task<Notificacion> CrearAsync(string pacienteId, string titulo, string mensaje,
        string tipo, string? cuidadorId = null, string? usuarioWebId = null)
    {
        var notificacion = new Notificacion
        {
            PacienteId = pacienteId,
            CuidadorId = cuidadorId,
            UsuarioWebId = usuarioWebId,
            Titulo = titulo,
            Mensaje = mensaje,
            Tipo = tipo,
            Leida = false,
            Enviada = false,
            FechaEnvio = DateTime.UtcNow
        };

        await _db.Notificaciones.InsertOneAsync(notificacion);
        return notificacion;
    }
}
