using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 2: Facturación y Pagos
/// ENDPOINT WEB
/// </summary>
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

    // ── Sesiones de pago ──────────────────────────────────────

    /// <summary>
    /// POST /api/Pagos/crear-sesion [WEB]
    /// MÓDULO 2: Crear sesión de pago en Stripe/PayPal
    /// </summary>
    [HttpPost("crear-sesion")]
    public async Task<IActionResult> CrearSesion([FromBody] CrearSesionPagoRequest request)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Creating payment session for user {UsuarioId}, plan {PlanNombre}", usuarioId, request.PlanNombre);
        var pago = await _pagosService.CrearSesionAsync(usuarioId, request.PlanNombre);
        if (pago == null)
        {
            _logger.LogWarning("Invalid plan {PlanNombre} for payment session by user {UsuarioId}", request.PlanNombre, usuarioId);
            return BadRequest(new { message = "Plan no válido" });
        }

        return Ok(new
        {
            PagoId = pago.Id,
            pago.StripeSessionId,
            pago.Monto,
            pago.Moneda,
            message = "Sesión de pago creada"
        });
    }

    // ── Historial ─────────────────────────────────────────────

    /// <summary>
    /// GET /api/Pagos/historial [WEB]
    /// MÓDULO 2: Historial de pagos del usuario
    /// </summary>
    [HttpGet("historial")]
    public async Task<IActionResult> Historial()
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Getting payment history for user {UsuarioId}", usuarioId);
        var pagos = await _pagosService.ObtenerHistorialAsync(usuarioId);
        var response = pagos.Select(p => new PagoResponse(
            p.Id, p.Monto, p.Moneda, p.Estado, p.FechaPago, p.MetodoPago));
        return Ok(response);
    }

    /// <summary>
    /// GET /api/Pagos/{id}/recibo [WEB]
    /// MÓDULO 2: Descargar recibo/factura PDF
    /// </summary>
    [HttpGet("{id}/recibo")]
    public async Task<IActionResult> Recibo(string id)
    {
        _logger.LogInformation("Getting receipt for payment {Id}", id);
        var pago = await _pagosService.ObtenerPorIdAsync(id);
        if (pago == null)
        {
            _logger.LogWarning("Payment {Id} not found when getting receipt", id);
            return NotFound();
        }

        return Ok(new
        {
            PagoId = pago.Id,
            pago.Monto,
            pago.Moneda,
            pago.Estado,
            pago.FechaPago,
            DescargaUrl = $"/api/pagos/{id}/recibo/descarga"
        });
    }

    // ── Cancelación ───────────────────────────────────────────

    /// <summary>
    /// POST /api/Pagos/cancelar [WEB]
    /// MÓDULO 2: Cancelar suscripción activa
    /// </summary>
    [HttpPost("cancelar")]
    public async Task<IActionResult> Cancelar()
    {
        var usuarioId = User.FindFirst("sub")?.Value;
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
}
