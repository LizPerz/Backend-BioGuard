using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using BioGuard.Api.Config;
using BioGuard.Api.Services;
namespace Test1BioGuard.UnitTests;
public class PaymentGatewayFactoryTests
{
    private readonly StripePaymentGateway _stripe;
    private readonly MercadoPagoPaymentGateway _mercadoPago;
    private readonly PaymentGatewayFactory _factory;
    public PaymentGatewayFactoryTests()
    {
        var stripeOptions = new Mock<IOptions<StripeOptions>>();
        stripeOptions.Setup(o => o.Value).Returns(new StripeOptions());
        var loggerStripe = new Mock<ILogger<StripePaymentGateway>>();
        _stripe = new StripePaymentGateway(stripeOptions.Object, loggerStripe.Object);
        var mpOptions = new Mock<IOptions<MercadoPagoOptions>>();
        mpOptions.Setup(o => o.Value).Returns(new MercadoPagoOptions());
        var loggerMp = new Mock<ILogger<MercadoPagoPaymentGateway>>();
        var httpClient = new HttpClient();
        _mercadoPago = new MercadoPagoPaymentGateway(mpOptions.Object, loggerMp.Object, httpClient);
        _factory = new PaymentGatewayFactory(_stripe, _mercadoPago);
    }
    [Fact]
    public void GetGateway_Stripe_ReturnsStripeGateway()
    {
        var gateway = _factory.GetGateway("stripe");
        gateway.Should().BeSameAs(_stripe);
    }
    [Fact]
    public void GetGateway_MercadoPago_ReturnsMercadoPagoGateway()
    {
        var gateway = _factory.GetGateway("mercadopago");
        gateway.Should().BeSameAs(_mercadoPago);
    }
    [Fact]
    public void GetGateway_MercadoPagoVariantes_ReturnsMercadoPagoGateway()
    {
        _factory.GetGateway("mercado_pago").Should().BeSameAs(_mercadoPago);
        _factory.GetGateway("mercado pago").Should().BeSameAs(_mercadoPago);
    }
    [Fact]
    public void GetGateway_MetodoInvalido_LanzaExcepcion()
    {
        var act = () => _factory.GetGateway("paypal");
        act.Should().Throw<ArgumentException>();
    }
    [Fact]
    public void GetGateway_CaseInsensitive_Works()
    {
        _factory.GetGateway("STRIPE").Should().BeSameAs(_stripe);
        _factory.GetGateway("MercadoPago").Should().BeSameAs(_mercadoPago);
    }
}
