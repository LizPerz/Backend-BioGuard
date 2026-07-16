using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class PagosService
{
    private readonly IMongoDbContext _db;

    public PagosService(IMongoDbContext db) => _db = db;

    public async Task<Pago?> CrearSesionAsync(string usuarioId, string planNombre)
    {
        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Nombre == planNombre);
        if (plan == null) return null;

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
        return pago;
    }

    public async Task<List<Pago>> ObtenerHistorialAsync(string usuarioId)
    {
        var filter = Builders<Pago>.Filter.Eq(p => p.UsuarioWebId, usuarioId);
        var sort = Builders<Pago>.Sort.Descending(p => p.FechaPago);
        return await _db.FindToListAsync(_db.Pagos, filter, sort);
    }

    public async Task<Pago?> ObtenerPorIdAsync(string pagoId)
    {
        return await _db.FindFirstOrDefaultAsync(_db.Pagos, p => p.Id == pagoId);
    }

    public async Task<bool> CancelarAsync(string usuarioId)
    {
        var filter = Builders<Pago>.Filter.And(
            Builders<Pago>.Filter.Eq(p => p.UsuarioWebId, usuarioId),
            Builders<Pago>.Filter.Eq(p => p.Estado, "completado"));
        var sort = Builders<Pago>.Sort.Descending(p => p.FechaPago);
        var pago = await _db.FindFirstOrDefaultAsync(_db.Pagos, filter, sort);

        if (pago == null) return false;

        var update = Builders<Pago>.Update.Set(p => p.Estado, "cancelado");
        var result = await _db.Pagos.UpdateOneAsync(p => p.Id == pago.Id, update);
        return result.ModifiedCount > 0;
    }
}
