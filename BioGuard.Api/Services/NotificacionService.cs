using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class NotificacionService
{
    private readonly IMongoDbContext _db;
    private readonly ILogger<NotificacionService> _logger;

    public NotificacionService(IMongoDbContext db, ILogger<NotificacionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<Notificacion>> ObtenerPorPacienteAsync(string pacienteId)
    {
        _logger.LogInformation("Obteniendo notificaciones para paciente {PacienteId}", pacienteId);
        var filter = Builders<Notificacion>.Filter.Eq(n => n.PacienteId, pacienteId);
        var sort = Builders<Notificacion>.Sort.Descending(n => n.FechaEnvio);
        return await _db.FindToListAsync(_db.Notificaciones, filter, sort, 50);
    }

    public async Task<List<Notificacion>> ObtenerPorUsuarioAsync(string usuarioWebId)
    {
        _logger.LogInformation("Obteniendo notificaciones para usuario {UsuarioWebId}", usuarioWebId);
        var filter = Builders<Notificacion>.Filter.Eq(n => n.UsuarioWebId, usuarioWebId);
        var sort = Builders<Notificacion>.Sort.Descending(n => n.FechaEnvio);
        return await _db.FindToListAsync(_db.Notificaciones, filter, sort, 50);
    }

    public async Task<bool> MarcarLeidaAsync(string notificacionId)
    {
        _logger.LogInformation("Marcando notificación como leída: {NotificacionId}", notificacionId);
        var update = Builders<Notificacion>.Update.Set(n => n.Leida, true);
        var result = await _db.Notificaciones.UpdateOneAsync(n => n.Id == notificacionId, update);
        if (result.ModifiedCount == 0)
            _logger.LogWarning("Notificación no encontrada o ya leída: {NotificacionId}", notificacionId);
        return result.ModifiedCount > 0;
    }

    public async Task<Notificacion> CrearAsync(string pacienteId, string titulo, string mensaje,
        string tipo, string? cuidadorId = null, string? usuarioWebId = null)
    {
        _logger.LogInformation("Creando notificación para paciente {PacienteId}, tipo: {Tipo}", pacienteId, tipo);
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
        _logger.LogInformation("Notificación creada con ID {NotificacionId}", notificacion.Id);
        return notificacion;
    }

    public async Task<bool> EliminarAsync(string notificacionId)
    {
        _logger.LogInformation("Eliminando notificación {NotificacionId}", notificacionId);
        var result = await _db.Notificaciones.DeleteOneAsync(n => n.Id == notificacionId);
        if (result.DeletedCount == 0)
            _logger.LogWarning("Notificación no encontrada para eliminar: {NotificacionId}", notificacionId);
        return result.DeletedCount > 0;
    }
}
