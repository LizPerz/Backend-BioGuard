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
    private readonly PaymentGatewayFactory _factory;
    public PaymentGatewayFactoryTests()
    {
        var stripeOptions = new Mock<IOptions<StripeOptions>>();
        stripeOptions.Setup(o => o.Value).Returns(new StripeOptions());
        var loggerStripe = new Mock<ILogger<StripePaymentGateway>>();
        _stripe = new StripePaymentGateway(stripeOptions.Object, loggerStripe.Object);
        _factory = new PaymentGatewayFactory(_stripe);
    }
    [Fact]
    public void GetGateway_Stripe_ReturnsStripeGateway()
    {
        var gateway = _factory.GetGateway("stripe");
        gateway.Should().BeSameAs(_stripe);
    }
    [Fact]
    public void GetGateway_SinMetodo_ReturnsStripeGateway()
    {
        var gateway = _factory.GetGateway(null);
        gateway.Should().BeSameAs(_stripe);
    }
    [Fact]
    public void GetGateway_MetodoInvalido_LanzaExcepcion()
    {
        var act = () => _factory.GetGateway("paypal");
        act.Should().Throw<ArgumentException>();
    }
    [Fact]
    public void GetGateway_MercadoPago_LanzaExcepcion()
    {
        var act = () => _factory.GetGateway("mercadopago");
        act.Should().Throw<ArgumentException>();
    }
    [Fact]
    public void GetGateway_CaseInsensitive_Works()
    {
        _factory.GetGateway("STRIPE").Should().BeSameAs(_stripe);
    }
}
