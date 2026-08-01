using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class PagosService
{
    private readonly IMongoDbContext _db;
    private readonly ILogger<PagosService> _logger;
    private readonly PaymentGatewayFactory _gatewayFactory;
    private readonly UsuariosWebService _usuariosWebService;
    private readonly IConfiguration _configuration;

    public PagosService(IMongoDbContext db, ILogger<PagosService> logger, PaymentGatewayFactory gatewayFactory, UsuariosWebService usuariosWebService, IConfiguration configuration)
    {
        _db = db;
        _logger = logger;
        _gatewayFactory = gatewayFactory;
        _usuariosWebService = usuariosWebService;
        _configuration = configuration;
    }

    public async Task<Pago?> CrearSesionAsync(string usuarioId, string planNombre, string? metodoPago = null)
    {
        _logger.LogInformation("Creando sesión de pago para usuario {UsuarioId}, plan {Plan}, método {Metodo}", usuarioId, planNombre, metodoPago ?? "ninguno");
        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Nombre == planNombre);
        if (plan == null)
        {
            _logger.LogWarning("Plan no encontrado: {PlanNombre}", planNombre);
            return null;
        }

        if (plan.Precio <= 0)
        {
            _logger.LogInformation("Plan gratuito para usuario {UsuarioId}, activando directamente", usuarioId);
            var pago = new Pago
            {
                UsuarioWebId = usuarioId,
                Monto = 0,
                Moneda = plan.PrecioMoneda,
                PlanId = plan.Id,
                Estado = "completado",
                FechaPago = DateTime.UtcNow,
                MetodoPago = "gratis",
                Gateway = "ninguno"
            };
            await _db.Pagos.InsertOneAsync(pago);

            await _usuariosWebService.CambiarPlanAsync(usuarioId, planNombre);
            _logger.LogInformation("Plan gratuito activado para usuario {UsuarioId}", usuarioId);
            return pago;
        }

        var metodo = !string.IsNullOrWhiteSpace(metodoPago) ? metodoPago : "stripe";
        if (!string.Equals(metodo, "stripe", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Método de pago no soportado: {Metodo}", metodo);
            return null;
        }
        var gateway = _gatewayFactory.GetGateway("stripe");

        var configSection = _configuration.GetSection("CallbackUrls");
        var successUrl = configSection["SuccessUrl"] ?? "http://localhost:3000/pago/exito";
        var cancelUrl = configSection["CancelUrl"] ?? "http://localhost:3000/pago/cancelado";

        var sessionResult = await gateway.CreateCheckoutSessionAsync(usuarioId, plan, successUrl, cancelUrl);
        if (!sessionResult.Success)
        {
            _logger.LogError("Error creando sesión de pago con {Metodo}: {Error}", metodo, sessionResult.Error);
            return null;
        }

        var pagoPago = new Pago
        {
            UsuarioWebId = usuarioId,
            Monto = plan.Precio,
            Moneda = plan.PrecioMoneda,
            PlanId = plan.Id,
            StripeSessionId = sessionResult.SessionId,
            CheckoutUrl = sessionResult.SessionUrl,
            Estado = "pendiente",
            FechaPago = DateTime.UtcNow,
            MetodoPago = metodo,
            Gateway = metodo
        };

        await _db.Pagos.InsertOneAsync(pagoPago);
        _logger.LogInformation("Sesión de pago creada con {Metodo}, ID {PagoId}", metodo, pagoPago.Id);
        return pagoPago;
    }

    public async Task<bool> ProcesarWebhookStripeAsync(string rawBody, string signature)
    {
        var gateway = _gatewayFactory.GetGateway("stripe");
        if (!await gateway.VerifyWebhookSignatureAsync(rawBody, signature))
        {
            _logger.LogWarning("Stripe webhook signature verification failed");
            return false;
        }

        var evento = await gateway.ParseWebhookEventAsync(rawBody, signature);
        if (evento.Status != "completado" || string.IsNullOrEmpty(evento.SessionId))
        {
            _logger.LogInformation("Stripe webhook ignorado: {Type} -> {Status}", evento.Type, evento.Status);
            return true;
        }

        return await ConfirmarPagoAsync(evento.SessionId, "stripe", evento.SubscriptionId, evento.PlanId);
    }

    private async Task<bool> ConfirmarPagoAsync(string sessionId, string gateway, string? subscriptionId, string? planId)
    {
        var filter = Builders<Pago>.Filter.Eq(p => p.StripeSessionId, sessionId);

        var pago = await _db.FindFirstOrDefaultAsync(_db.Pagos, filter);
        if (pago == null)
        {
            _logger.LogWarning("Pago no encontrado para sesión {SessionId} ({Gateway})", sessionId, gateway);
            return false;
        }

        if (pago.Estado == "completado")
        {
            _logger.LogInformation("Pago {PagoId} ya está completado", pago.Id);
            return true;
        }

        var update = Builders<Pago>.Update
            .Set(p => p.Estado, "completado")
            .Set(p => p.FechaPago, DateTime.UtcNow);

        if (!string.IsNullOrEmpty(subscriptionId))
            update = update.Set(p => p.StripeSessionId, subscriptionId);

        await _db.Pagos.UpdateOneAsync(p => p.Id == pago.Id, update);

        var usuario = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Id == pago.UsuarioWebId);
        if (usuario != null)
        {
            var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Id == (planId ?? pago.PlanId));
            if (plan != null)
                await _usuariosWebService.CambiarPlanAsync(pago.UsuarioWebId, plan.Nombre);
        }

        _logger.LogInformation("Pago {PagoId} confirmado y plan activado para usuario {UsuarioId}", pago.Id, pago.UsuarioWebId);
        return true;
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

        var gateway = _gatewayFactory.GetGateway(pago.Gateway);
        var gatewayOk = await gateway.CancelSubscriptionAsync(pago.StripeSessionId ?? "");

        if (!gatewayOk)
        {
            _logger.LogWarning("No se pudo cancelar en la pasarela {Gateway}", pago.Gateway);
            return false;
        }

        var update = Builders<Pago>.Update.Set(p => p.Estado, "cancelado");
        var result = await _db.Pagos.UpdateOneAsync(p => p.Id == pago.Id, update);
        _logger.LogInformation("Pago {PagoId} cancelado", pago.Id);
        return result.ModifiedCount > 0;
    }
}
