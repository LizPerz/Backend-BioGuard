using Microsoft.Extensions.Logging;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class PayPalPaymentGateway : IPaymentGateway
{
    private readonly ILogger<PayPalPaymentGateway> _logger;

    public PayPalPaymentGateway(ILogger<PayPalPaymentGateway> logger)
    {
        _logger = logger;
    }

    public Task<PaymentSessionResult> CreateCheckoutSessionAsync(string usuarioId, Plan plan, string successUrl, string cancelUrl)
    {
        _logger.LogWarning("PayPal gateway en mantenimiento. Usuario {UsuarioId} intentó crear sesión.", usuarioId);
        return Task.FromResult(new PaymentSessionResult(false, null, null, null, "PayPal está en mantenimiento. Usa Stripe."));
    }

    public Task<bool> VerifyWebhookSignatureAsync(string payload, IReadOnlyDictionary<string, string> headers)
    {
        return Task.FromResult(false);
    }

    public async Task<PaymentWebhookEvent> ParseWebhookEventAsync(string payload, IReadOnlyDictionary<string, string> headers)
    {
        return new PaymentWebhookEvent("", "", null, null, null, "error", null);
    }

    public Task<bool> CancelSubscriptionAsync(string subscriptionId)
    {
        return Task.FromResult(false);
    }
}