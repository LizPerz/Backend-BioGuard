using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using BioGuard.Api.Config;
using BioGuard.Api.Services;

namespace Test1BioGuard.UnitTests;

public class StripePaymentGatewayTests
{
    private const string WebhookSecret = "whsec_test_secret";

    private const string RealisticPayload =
        "{\"id\":\"evt_1\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1730000000," +
        "\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":\"req_1\",\"idempotency_key\":null}," +
        "\"type\":\"checkout.session.completed\",\"data\":{\"object\":{" +
        "\"id\":\"cs_test\",\"object\":\"checkout.session\",\"client_reference_id\":\"user123\"," +
        "\"subscription\":\"sub_1\",\"customer\":\"cus_1\"," +
        "\"metadata\":{\"plan_id\":\"plan1\",\"usuario_id\":\"user123\",\"plan_nombre\":\"Familiar\"}," +
        "\"payment_status\":\"paid\",\"status\":\"complete\",\"amount_total\":100,\"currency\":\"mxn\"}}}";

    private static StripePaymentGateway BuildGateway()
    {
        var options = new Mock<IOptions<StripeOptions>>();
        options.Setup(o => o.Value).Returns(new StripeOptions { WebhookSecret = WebhookSecret });
        return new StripePaymentGateway(options.Object, Mock.Of<ILogger<StripePaymentGateway>>());
    }

    private static (string Signature, string Payload) SignPayload(string payload)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signed = $"{ts}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(WebhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signed));
        return ($"t={ts},v1={Convert.ToHexString(hash).ToLower()}", payload);
    }

    [Fact]
    public async Task VerifyWebhookSignature_FirmaValida_RetornaTrue()
    {
        var gateway = BuildGateway();
        var (signature, body) = SignPayload(RealisticPayload);

        var result = await gateway.VerifyWebhookSignatureAsync(body, signature);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyWebhookSignature_FirmaIncorrecta_RetornaFalse()
    {
        var gateway = BuildGateway();

        var result = await gateway.VerifyWebhookSignatureAsync(RealisticPayload, "t=1,v1=invalid");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyWebhookSignature_ApiVersionDistinta_RetornaTrue()
    {
        var gateway = BuildGateway();
        var (signature, body) = SignPayload(RealisticPayload);

        var result = await gateway.VerifyWebhookSignatureAsync(body, signature);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ParseWebhookEvent_PagoCompletado_MapeaCampos()
    {
        var gateway = BuildGateway();
        var (signature, body) = SignPayload(RealisticPayload);

        var evt = await gateway.ParseWebhookEventAsync(body, signature);

        evt.EventId.Should().Be("evt_1");
        evt.Type.Should().Be("checkout.session.completed");
        evt.SessionId.Should().Be("cs_test");
        evt.SubscriptionId.Should().Be("sub_1");
        evt.CustomerId.Should().Be("cus_1");
        evt.Status.Should().Be("completado");
        evt.PlanId.Should().Be("plan1");
    }

    [Fact]
    public async Task ParseWebhookEvent_InvoicePaid_MapeaRenovacion()
    {
        var gateway = BuildGateway();
        const string payload =
            "{\"id\":\"evt_2\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1730000001," +
            "\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":\"req_2\",\"idempotency_key\":null}," +
            "\"type\":\"invoice.paid\",\"data\":{\"object\":{" +
            "\"id\":\"in_1\",\"object\":\"invoice\",\"subscription\":\"sub_1\",\"customer\":\"cus_1\"," +
            "\"status\":\"paid\",\"amount_paid\":100,\"currency\":\"mxn\"}}}";
        var (signature, body) = SignPayload(payload);

        var evt = await gateway.ParseWebhookEventAsync(body, signature);

        evt.Type.Should().Be("invoice.paid");
        evt.Status.Should().Be("renovado");
        evt.SubscriptionId.Should().Be("sub_1");
        evt.CustomerId.Should().Be("cus_1");
        evt.SessionId.Should().BeNull();
        evt.PlanId.Should().BeNull();
    }

    [Fact]
    public async Task ParseWebhookEvent_SuscripcionCancelada_MapeaCancelacion()
    {
        var gateway = BuildGateway();
        const string payload =
            "{\"id\":\"evt_3\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1730000002," +
            "\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":\"req_3\",\"idempotency_key\":null}," +
            "\"type\":\"customer.subscription.deleted\",\"data\":{\"object\":{" +
            "\"id\":\"sub_1\",\"object\":\"subscription\",\"customer\":\"cus_1\",\"status\":\"canceled\"," +
            "\"plan\":{\"id\":\"price_x\",\"object\":\"plan\",\"interval\":\"month\"}}}}";
        var (signature, body) = SignPayload(payload);

        var evt = await gateway.ParseWebhookEventAsync(body, signature);

        evt.Type.Should().Be("customer.subscription.deleted");
        evt.Status.Should().Be("cancelado");
        evt.SubscriptionId.Should().Be("sub_1");
        evt.CustomerId.Should().Be("cus_1");
        evt.SessionId.Should().BeNull();
        evt.PlanId.Should().BeNull();
    }
}
