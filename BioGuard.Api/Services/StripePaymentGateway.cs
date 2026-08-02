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
    }

    public async Task<PaymentSessionResult> CreateCheckoutSessionAsync(string usuarioId, PlanModel plan, string successUrl, string cancelUrl)
    {
        try
        {
            StripeConfiguration.ApiKey = _options.SecretKey;

            var sessionOptions = new SessionCreateOptions
            {
                CustomerEmail = null,
                ClientReferenceId = usuarioId,
                Metadata = new Dictionary<string, string>
                {
                    { "usuario_id", usuarioId },
                    { "plan_id", plan.Id },
                    { "plan_nombre", plan.Nombre }
                },
                Mode = plan.Precio > 0 ? "subscription" : "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl
            };

            if (!string.IsNullOrEmpty(plan.StripePriceId))
            {
                sessionOptions.LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = plan.StripePriceId,
                        Quantity = 1
                    }
                };
            }
            else
            {
                sessionOptions.LineItems = new List<SessionLineItemOptions>
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
                };
            }

            var service = new SessionService();
            var session = await service.CreateAsync(sessionOptions);

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
            EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret, 300, throwOnApiVersionMismatch: false);
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
            var stripeEvent = EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret, 300, throwOnApiVersionMismatch: false);

            var status = stripeEvent.Type switch
            {
                "checkout.session.completed" => "completado",
                "checkout.session.expired" => "expirado",
                "invoice.paid" => "renovado",
                "customer.subscription.deleted" => "cancelado",
                _ => "desconocido"
            };

            string? sessionId = null;
            string? subscriptionId = null;
            string? customerId = null;
            string? planId = null;

            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                case "checkout.session.expired":
                    if (stripeEvent.Data.Object is Session session)
                    {
                        sessionId = session.Id;
                        subscriptionId = session.SubscriptionId;
                        customerId = session.CustomerId;
                        planId = session.Metadata?.GetValueOrDefault("plan_id");
                    }
                    break;
                case "invoice.paid":
                    if (stripeEvent.Data.Object is Invoice invoice)
                    {
                        subscriptionId = invoice.SubscriptionId;
                        customerId = invoice.CustomerId;
                    }
                    break;
                case "customer.subscription.deleted":
                    if (stripeEvent.Data.Object is Subscription subscription)
                    {
                        subscriptionId = subscription.Id;
                        customerId = subscription.CustomerId;
                    }
                    break;
            }

            return Task.FromResult(new PaymentWebhookEvent(
                EventId: stripeEvent.Id,
                Type: stripeEvent.Type,
                SessionId: sessionId,
                SubscriptionId: subscriptionId,
                CustomerId: customerId,
                Status: status,
                PlanId: planId
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
            StripeConfiguration.ApiKey = _options.SecretKey;
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
