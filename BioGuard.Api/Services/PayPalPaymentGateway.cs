using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using PlanModel = BioGuard.Api.Models.Plan;

namespace BioGuard.Api.Services;

public class PayPalPaymentGateway : IPaymentGateway
{
    private readonly ILogger<PayPalPaymentGateway> _logger;
    private readonly PayPalOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);
    private static PayPalToken? _cachedToken;

    public PayPalPaymentGateway(IOptions<PayPalOptions> options, ILogger<PayPalPaymentGateway> logger, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    private string BaseUrl => _options.SandboxMode ? "https://api-m.sandbox.paypal.com" : "https://api-m.paypal.com";

    public async Task<PaymentSessionResult> CreateCheckoutSessionAsync(string usuarioId, PlanModel plan, string successUrl, string cancelUrl)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            if (token == null)
                return new PaymentSessionResult(false, null, null, null, "No se pudo autenticar con PayPal");

            var client = _httpClientFactory.CreateClient("PayPal");
            var orderRequest = new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        reference_id = usuarioId,
                        description = $"BioGuard - Plan {plan.Nombre}",
                        custom_id = plan.Id,
                        amount = new
                        {
                            currency_code = plan.PrecioMoneda.ToUpper(),
                            value = plan.Precio.ToString("F2")
                        },
                        items = new[]
                        {
                            new
                            {
                                name = $"Plan {plan.Nombre}",
                                description = plan.Descripcion,
                                unit_amount = new
                                {
                                    currency_code = plan.PrecioMoneda.ToUpper(),
                                    value = plan.Precio.ToString("F2")
                                },
                                quantity = "1",
                                category = "DIGITAL_GOODS"
                            }
                        }
                    }
                },
                payment_source = new
                {
                    paypal = new
                    {
                        experience_context = new
                        {
                            payment_method_preference = "IMMEDIATE_PAYMENT_REQUIRED",
                            landing_page = "LOGIN",
                            user_action = "PAY_NOW",
                            return_url = successUrl,
                            cancel_url = cancelUrl
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(orderRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/checkout/orders")
            {
                Content = content,
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken) }
            };

            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PayPal order creation failed: {Status} {Body}", response.StatusCode, responseBody);
                return new PaymentSessionResult(false, null, null, null, "Error creando orden PayPal");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var orderId = doc.RootElement.GetProperty("id").GetString()!;
            var approveLink = doc.RootElement.GetProperty("links")
                .EnumerateArray()
                .FirstOrDefault(l => l.GetProperty("rel").GetString() == "payer-action")
                .GetProperty("href").GetString();

            _logger.LogInformation("PayPal order created: {OrderId}", orderId);
            return new PaymentSessionResult(true, orderId, approveLink, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayPal error creating checkout session");
            return new PaymentSessionResult(false, null, null, null, ex.Message);
        }
    }

    public async Task<bool> VerifyWebhookSignatureAsync(string payload, string signature)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            if (token == null) return false;

            var client = _httpClientFactory.CreateClient("PayPal");
            var verifyRequest = new
            {
                auth_algo = "",
                cert_url = "",
                transmission_id = signature,
                transmission_sig = "",
                transmission_time = DateTime.UtcNow.ToString("O"),
                webhook_id = _options.WebhookId,
                webhook_event = JsonDocument.Parse(payload).RootElement
            };

            var json = JsonSerializer.Serialize(verifyRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/notifications/verify-webhook-signature")
            {
                Content = content,
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken) }
            };

            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return false;

            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.GetProperty("verification_status").GetString() == "SUCCESS";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayPal webhook signature verification failed");
            return false;
        }
    }

    public async Task<PaymentWebhookEvent> ParseWebhookEventAsync(string payload, string signature)
    {
        try
        {
            var verified = await VerifyWebhookSignatureAsync(payload, signature);
            if (!verified)
                return new PaymentWebhookEvent("", "", null, null, null, "error", null);

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var eventId = root.GetProperty("id").GetString() ?? "";
            var eventType = root.GetProperty("event_type").GetString() ?? "";

            string? sessionId = null;
            string? subscriptionId = null;
            string? status = "desconocido";

            if (root.TryGetProperty("resource", out var resource))
            {
                if (resource.TryGetProperty("id", out var resId))
                {
                    if (eventType.Contains("ORDER"))
                        sessionId = resId.GetString();
                    else if (eventType.Contains("SUBSCRIPTION"))
                        subscriptionId = resId.GetString();
                }

                if (resource.TryGetProperty("status", out var resStatus))
                    status = resStatus.GetString();
            }

            status = eventType switch
            {
                "CHECKOUT.ORDER.APPROVED" => "completado",
                "CHECKOUT.ORDER.CAPTURED" => "capturado",
                "PAYMENT.CAPTURE.COMPLETED" => "completado",
                "PAYMENT.CAPTURE.DENIED" => "denegado",
                "BILLING.SUBSCRIPTION.CANCELLED" => "cancelado",
                "BILLING.SUBSCRIPTION.SUSPENDED" => "suspendido",
                "BILLING.SUBSCRIPTION.ACTIVATED" => "activado",
                _ => status ?? "desconocido"
            };

            return new PaymentWebhookEvent(eventId, eventType, sessionId, subscriptionId, null, status, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing PayPal webhook");
            return new PaymentWebhookEvent("", "", null, null, null, "error", null);
        }
    }

    public async Task<bool> CancelSubscriptionAsync(string subscriptionId)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            if (token == null) return false;

            var client = _httpClientFactory.CreateClient("PayPal");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/billing/subscriptions/{subscriptionId}/cancel")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken) }
            };

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("PayPal cancel subscription failed: {Status} {Body}", response.StatusCode, body);
                return false;
            }

            _logger.LogInformation("PayPal subscription cancelled: {SubscriptionId}", subscriptionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling PayPal subscription");
            return false;
        }
    }

    private async Task<PayPalToken?> GetAccessTokenAsync()
    {
        if (_cachedToken != null && _cachedToken.ExpiresAt > DateTime.UtcNow)
            return _cachedToken;

        await _tokenLock.WaitAsync();
        try
        {
            if (_cachedToken != null && _cachedToken.ExpiresAt > DateTime.UtcNow)
                return _cachedToken;

            var client = _httpClientFactory.CreateClient("PayPal");
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/oauth2/token")
            {
                Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded"),
                Headers = { Authorization = new AuthenticationHeaderValue("Basic", credentials) }
            };

            var response = await client.SendAsync(tokenRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PayPal auth failed: {Status} {Body}", response.StatusCode, responseBody);
                return null;
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            _cachedToken = new PayPalToken
            {
                AccessToken = root.GetProperty("access_token").GetString()!,
                ExpiresAt = DateTime.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32() - 300)
            };

            return _cachedToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting PayPal access token");
            return null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private class PayPalToken
    {
        public string AccessToken { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
    }
}