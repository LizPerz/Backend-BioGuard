using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public interface IPaymentGateway
{
    Task<PaymentSessionResult> CreateCheckoutSessionAsync(string usuarioId, Plan plan, string successUrl, string cancelUrl);
    Task<bool> VerifyWebhookSignatureAsync(string payload, IReadOnlyDictionary<string, string> headers);
    Task<PaymentWebhookEvent> ParseWebhookEventAsync(string payload, IReadOnlyDictionary<string, string> headers);
    Task<bool> CancelSubscriptionAsync(string subscriptionId);
}

public record PaymentSessionResult(
    bool Success,
    string? SessionId,
    string? SessionUrl,
    string? SubscriptionId,
    string? Error);

public record PaymentWebhookEvent(
    string EventId,
    string Type,
    string? SessionId,
    string? SubscriptionId,
    string? CustomerId,
    string Status,
    string? PlanId);
