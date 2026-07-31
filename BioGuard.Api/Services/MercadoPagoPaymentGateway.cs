using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using PlanModel = BioGuard.Api.Models.Plan;

namespace BioGuard.Api.Services;

public class MercadoPagoPaymentGateway : IPaymentGateway
{
    private readonly ILogger<MercadoPagoPaymentGateway> _logger;
    private readonly MercadoPagoOptions _options;
    private readonly HttpClient _httpClient;

    public MercadoPagoPaymentGateway(IOptions<MercadoPagoOptions> options, ILogger<MercadoPagoPaymentGateway> logger, HttpClient httpClient)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;
    }

    private string BaseUrl => "https://api.mercadopago.com";

    private void SetAuthHeader(HttpRequestMessage request)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);
    }

    public async Task<PaymentSessionResult> CreateCheckoutSessionAsync(string usuarioId, PlanModel plan, string successUrl, string cancelUrl)
    {
        try
        {
            var preference = new
            {
                external_reference = usuarioId,
                back_urls = new
                {
                    success = successUrl,
                    failure = cancelUrl,
                    pending = cancelUrl
                },
                auto_return = "approved",
                notification_url = _options.WebhookUrl,
                items = new[]
                {
                    new
                    {
                        id = plan.Id,
                        title = $"BioGuard - Plan {plan.Nombre}",
                        description = plan.Descripcion,
                        quantity = 1,
                        currency_id = plan.PrecioMoneda.ToUpper(),
                        unit_price = (double)plan.Precio
                    }
                },
                metadata = new
                {
                    usuario_id = usuarioId,
                    plan_id = plan.Id,
                    plan_nombre = plan.Nombre
                },
                purpose = "wallet_purchase"
            };

            var json = JsonSerializer.Serialize(preference);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/checkout/preferences")
            {
                Content = content
            };
            SetAuthHeader(request);

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Mercado Pago preference creation failed: {Status} {Body}", response.StatusCode, responseBody);
                return new PaymentSessionResult(false, null, null, null, "Error creando preferencia en Mercado Pago");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var preferenceId = doc.RootElement.GetProperty("id").GetString()!;
            var initPoint = doc.RootElement.GetProperty("init_point").GetString()!;

            _logger.LogInformation("Mercado Pago preference created: {PreferenceId}", preferenceId);
            return new PaymentSessionResult(true, preferenceId, initPoint, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mercado Pago error creating checkout session");
            return new PaymentSessionResult(false, null, null, null, ex.Message);
        }
    }

    public async Task<bool> VerifyWebhookSignatureAsync(string payload, string signatureHeader)
    {
        try
        {
            if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(_options.ClientSecret))
            {
                _logger.LogWarning("Mercado Pago webhook signature verification skipped: missing header or ClientSecret");
                return false;
            }

            var parts = signatureHeader.Split(',')
                .Select(p => p.Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

            if (!parts.TryGetValue("ts", out var ts) || !parts.TryGetValue("v1", out var v1))
                return false;

            var dataId = "";
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var dataIdEl))
                dataId = dataIdEl.GetString() ?? "";

            var signaturePayload = $"{dataId}.{dataId}.{ts}.{payload}";
            var secretBytes = Encoding.UTF8.GetBytes(_options.ClientSecret);
            var payloadBytes = Encoding.UTF8.GetBytes(signaturePayload);

            var computedHash = HMACSHA256.HashData(secretBytes, payloadBytes);
            var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

            var isValid = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedHex),
                Encoding.UTF8.GetBytes(v1));

            _logger.LogInformation("Mercado Pago webhook signature valid: {IsValid}", isValid);
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mercado Pago webhook signature verification failed");
            return false;
        }
    }

    public Task<PaymentWebhookEvent> ParseWebhookEventAsync(string payload, string signature)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var eventId = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            var eventType = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "" : "";
            var action = root.TryGetProperty("action", out var actionEl) ? actionEl.GetString() ?? eventType : eventType;

            string? sessionId = null;
            string? status = "desconocido";

            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var dataId))
                sessionId = dataId.GetString();

            status = action switch
            {
                "payment.created" => "pendiente",
                "payment.approved" => "completado",
                "payment.rejected" => "rechazado",
                "payment.cancelled" => "cancelado",
                "payment.refunded" => "reembolsado",
                _ => status
            };

            string? planId = null;
            if (root.TryGetProperty("additional_info", out var info) && info.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var itemId))
                    {
                        planId = itemId.GetString();
                        break;
                    }
                }
            }

            return Task.FromResult(new PaymentWebhookEvent(eventId, action, sessionId, null, null, status, planId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Mercado Pago webhook");
            return Task.FromResult(new PaymentWebhookEvent("", "", null, null, null, "error", null));
        }
    }

    public async Task<bool> CancelSubscriptionAsync(string preferenceId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"{BaseUrl}/checkout/preferences/{preferenceId}")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { purpose = "wallet_purchase", active = false }),
                    Encoding.UTF8,
                    "application/json")
            };
            SetAuthHeader(request);

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Mercado Pago cancel failed: {Status} {Body}", response.StatusCode, body);
                return false;
            }

            _logger.LogInformation("Mercado Pago preference cancelled: {PreferenceId}", preferenceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling Mercado Pago preference");
            return false;
        }
    }
}
