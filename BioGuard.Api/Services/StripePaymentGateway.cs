using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using PlanModel = BioGuard.Api.Models.Plan;

namespace BioGuard.Api.Services;

public class StripePaymentGateway : IPaymentGateway
{
    private readonly ILogger<StripePaymentGateway> _logger;
    private readonly StripeOptions _options;

    public StripePaymentGateway(IOptions<StripeOptions> options, ILogger<StripePaymentGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
        StripeConfiguration.ApiKey = _options.SecretKey;
    }

    public async Task<PaymentSessionResult> CreateCheckoutSessionAsync(string usuarioId, PlanModel plan, string successUrl, string cancelUrl)
    {
        try
        {
            var options = new SessionCreateOptions
            {
                CustomerEmail = null,
                ClientReferenceId = usuarioId,
                Metadata = new Dictionary<string, string>
                {
                    { "usuario_id", usuarioId },
                    { "plan_id", plan.Id },
                    { "plan_nombre", plan.Nombre }
                },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = plan.PrecioMoneda.ToLower(),
                            UnitAmountDecimal = plan.Precio * 100,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"BioGuard - Plan {plan.Nombre}",
                                Description = plan.Descripcion
                            },
                            Recurring = plan.Precio > 0
                                ? new SessionLineItemPriceDataRecurringOptions { Interval = "month" }
                                : null
                        },
                        Quantity = 1
                    }
                },
                Mode = plan.Precio > 0 ? "subscription" : "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            _logger.LogInformation("Stripe checkout session created: {SessionId}", session.Id);
            return new PaymentSessionResult(true, session.Id, session.Url, session.SubscriptionId, null);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error creating checkout session");
            return new PaymentSessionResult(false, null, null, null, ex.Message);
        }
    }

    public Task<bool> VerifyWebhookSignatureAsync(string payload, string signature)
    {
        try
        {
            EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret);
            return Task.FromResult(true);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return Task.FromResult(false);
        }
    }

    public Task<PaymentWebhookEvent> ParseWebhookEventAsync(string payload, string signature)
    {
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret);
            var session = stripeEvent.Data.Object as Session;

            return Task.FromResult(new PaymentWebhookEvent(
                EventId: stripeEvent.Id,
                Type: stripeEvent.Type,
                SessionId: session?.Id,
                SubscriptionId: session?.SubscriptionId,
                CustomerId: session?.CustomerId,
                Status: stripeEvent.Type switch
                {
                    "checkout.session.completed" => "completado",
                    "checkout.session.expired" => "expirado",
                    "invoice.paid" => "renovado",
                    "customer.subscription.deleted" => "cancelado",
                    _ => "desconocido"
                },
                PlanId: session?.Metadata?.GetValueOrDefault("plan_id")
            ));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error parsing Stripe webhook event");
            return Task.FromResult(new PaymentWebhookEvent("", "", null, null, null, "error", null));
        }
    }

    public async Task<bool> CancelSubscriptionAsync(string subscriptionId)
    {
        try
        {
            var service = new SubscriptionService();
            await service.CancelAsync(subscriptionId);
            _logger.LogInformation("Stripe subscription cancelled: {SubscriptionId}", subscriptionId);
            return true;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Error cancelling Stripe subscription");
            return false;
        }
    }
}
