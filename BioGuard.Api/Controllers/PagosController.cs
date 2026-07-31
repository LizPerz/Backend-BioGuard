using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;

namespace BioGuard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagosController : ControllerBase
{
    private readonly PagosService _pagosService;
    private readonly ILogger<PagosController> _logger;

    public PagosController(PagosService pagosService, ILogger<PagosController> logger)
    {
        _pagosService = pagosService;
        _logger = logger;
    }

    [HttpPost("crear-sesion")]
    public async Task<IActionResult> CrearSesion([FromBody] CrearSesionPagoRequest request)
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Creating payment session for user {UsuarioId}, plan {PlanNombre}, method {MetodoPago}", usuarioId, request.PlanNombre, request.MetodoPago ?? "stripe");
        var pago = await _pagosService.CrearSesionAsync(usuarioId, request.PlanNombre, request.MetodoPago);
        if (pago == null)
        {
            _logger.LogWarning("Invalid plan {PlanNombre} for payment session by user {UsuarioId}", request.PlanNombre, usuarioId);
            return BadRequest(new { message = "Plan no válido" });
        }

        return Ok(new
        {
            PagoId = pago.Id,
            SessionId = pago.StripeSessionId ?? pago.MercadoPagoPreferenceId,
            pago.Gateway,
            pago.Monto,
            pago.Moneda,
            message = "Sesión de pago creada"
        });
    }

    [HttpGet("historial")]
    public async Task<IActionResult> Historial()
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Getting payment history for user {UsuarioId}", usuarioId);
        var pagos = await _pagosService.ObtenerHistorialAsync(usuarioId);
        var response = pagos.Select(p => new PagoResponse(
            p.Id, p.Monto, p.Moneda, p.Estado, p.FechaPago, p.MetodoPago));
        return Ok(response);
    }

    [HttpGet("{id}/recibo")]
    public async Task<IActionResult> Recibo(string id)
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Getting receipt for payment {Id}", id);
        var pago = await _pagosService.ObtenerPorIdAsync(id);
        if (pago == null)
        {
            _logger.LogWarning("Payment {Id} not found when getting receipt", id);
            return NotFound();
        }

        if (pago.UsuarioWebId != usuarioId)
        {
            _logger.LogWarning("User {UsuarioId} attempted to access receipt of payment {PaymentId} belonging to user {PaymentOwner}", usuarioId, id, pago.UsuarioWebId);
            return Forbid();
        }

        return Ok(new
        {
            PagoId = pago.Id,
            pago.Monto,
            pago.Moneda,
            pago.Estado,
            pago.FechaPago,
            pago.MetodoPago,
            Descripcion = $"BioGuard - Pago de ${pago.Monto} {pago.Moneda} ({pago.Estado})"
        });
    }

    [HttpPost("cancelar")]
    public async Task<IActionResult> Cancelar()
    {
        var usuarioId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Cancelling subscription for user {UsuarioId}", usuarioId);
        var result = await _pagosService.CancelarAsync(usuarioId);
        if (!result)
        {
            _logger.LogWarning("No active subscription to cancel for user {UsuarioId}", usuarioId);
            return BadRequest(new { message = "No hay suscripción activa" });
        }
        return Ok(new { message = "Suscripción cancelada" });
    }

    [AllowAnonymous]
    [HttpPost("webhook/stripe")]
    public async Task<IActionResult> WebhookStripe()
    {
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Stripe webhook received without signature");
            return BadRequest(new { message = "Missing Stripe-Signature header" });
        }

        string rawBody;
        using (var reader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8))
        {
            rawBody = await reader.ReadToEndAsync();
        }

        var result = await _pagosService.ProcesarWebhookStripeAsync(rawBody, signature);
        if (!result)
        {
            _logger.LogWarning("Stripe webhook processing failed");
            return StatusCode(500, new { message = "Webhook processing failed" });
        }

        _logger.LogInformation("Stripe webhook processed successfully");
        return Ok(new { received = true });
    }

    [AllowAnonymous]
    [HttpPost("webhook/mercadopago")]
    public async Task<IActionResult> WebhookMercadoPago()
    {
        var signature = Request.Headers["x-signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Mercado Pago webhook received without signature");
            return BadRequest(new { message = "Missing x-signature header" });
        }

        string rawBody;
        using (var reader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8))
        {
            rawBody = await reader.ReadToEndAsync();
        }

        var result = await _pagosService.ProcesarWebhookMercadoPagoAsync(rawBody, signature);
        if (!result)
        {
            _logger.LogWarning("Mercado Pago webhook processing failed");
            return StatusCode(500, new { message = "Webhook processing failed" });
        }

        _logger.LogInformation("Mercado Pago webhook processed successfully");
        return Ok(new { received = true });
    }
}
