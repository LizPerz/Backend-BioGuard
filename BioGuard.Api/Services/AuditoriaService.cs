using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class AuditoriaService
{
    private readonly IMongoDbContext _db;
    private readonly ILogger<AuditoriaService> _logger;

    public AuditoriaService(IMongoDbContext db, ILogger<AuditoriaService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<Auditoria>> ObtenerAsync(int pagina, int porPagina)
    {
        _logger.LogInformation("Obteniendo auditoría, página {Pagina}", pagina);
        var filter = Builders<Auditoria>.Filter.Empty;
        var sort = Builders<Auditoria>.Sort.Descending(a => a.Fecha);
        return await _db.FindToListAsync(_db.Auditoria, filter, sort, porPagina, (pagina - 1) * porPagina);
    }

    public async Task RegistrarAsync(string entidadId, string accion, string tabla, string registroId, string ip)
    {
        _logger.LogInformation("Registrando auditoría: {Accion} en {Tabla}, registro {RegistroId}", accion, tabla, registroId);
        var auditoria = new Auditoria
        {
            EntidadId = entidadId,
            Accion = accion,
            TablaAfectada = tabla,
            RegistroId = registroId,
            Fecha = DateTime.UtcNow,
            Ip = ip
        };

        await _db.Auditoria.InsertOneAsync(auditoria);
    }
}
