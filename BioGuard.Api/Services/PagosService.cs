using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class PagosService
{
    private readonly IMongoDbContext _db;
    private readonly ILogger<PagosService> _logger;

    public PagosService(IMongoDbContext db, ILogger<PagosService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Pago?> CrearSesionAsync(string usuarioId, string planNombre)
    {
        _logger.LogInformation("Creando sesión de pago para usuario {UsuarioId}, plan {Plan}", usuarioId, planNombre);
        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Nombre == planNombre);
        if (plan == null)
        {
            _logger.LogWarning("Plan no encontrado: {PlanNombre}", planNombre);
            return null;
        }

        var pago = new Pago
        {
            UsuarioWebId = usuarioId,
            Monto = plan.Precio,
            Moneda = plan.PrecioMoneda,
            PlanId = plan.Id,
            StripeSessionId = $"cs_{Guid.NewGuid():N}",
            StripeCustomerId = $"cus_{Guid.NewGuid():N}",
            Estado = "pendiente",
            FechaPago = DateTime.UtcNow,
            MetodoPago = "tarjeta"
        };

        await _db.Pagos.InsertOneAsync(pago);
        _logger.LogInformation("Sesión de pago creada con ID {PagoId}", pago.Id);
        return pago;
    }

    public async Task<List<Pago>> ObtenerHistorialAsync(string usuarioId)
    {
        _logger.LogInformation("Obteniendo historial de pagos para usuario {UsuarioId}", usuarioId);
        var filter = Builders<Pago>.Filter.Eq(p => p.UsuarioWebId, usuarioId);
        var sort = Builders<Pago>.Sort.Descending(p => p.FechaPago);
        return await _db.FindToListAsync(_db.Pagos, filter, sort);
    }

    public async Task<Pago?> ObtenerPorIdAsync(string pagoId)
    {
        _logger.LogInformation("Buscando pago {PagoId}", pagoId);
        return await _db.FindFirstOrDefaultAsync(_db.Pagos, p => p.Id == pagoId);
    }

    public async Task<bool> CancelarAsync(string usuarioId)
    {
        _logger.LogInformation("Cancelando pago para usuario {UsuarioId}", usuarioId);
        var filter = Builders<Pago>.Filter.And(
            Builders<Pago>.Filter.Eq(p => p.UsuarioWebId, usuarioId),
            Builders<Pago>.Filter.Eq(p => p.Estado, "completado"));
        var sort = Builders<Pago>.Sort.Descending(p => p.FechaPago);
        var pago = await _db.FindFirstOrDefaultAsync(_db.Pagos, filter, sort);

        if (pago == null)
        {
            _logger.LogWarning("No se encontró pago completado para cancelar, usuario {UsuarioId}", usuarioId);
            return false;
        }

        var update = Builders<Pago>.Update.Set(p => p.Estado, "cancelado");
        var result = await _db.Pagos.UpdateOneAsync(p => p.Id == pago.Id, update);
        _logger.LogInformation("Pago {PagoId} cancelado", pago.Id);
        return result.ModifiedCount > 0;
    }
}
