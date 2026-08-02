using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using BioGuard.Api.Services;
namespace Test1BioGuard.UnitTests;
public class PagosServiceTests
{
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<ILogger<PagosService>> _mockLogger;
    private readonly Mock<PaymentGatewayFactory> _mockFactory;
    private readonly Mock<UsuariosWebService> _mockUsuariosWebService;
    private readonly Mock<IMongoCollection<Pago>> _mockPagos;
    private readonly Mock<IMongoCollection<Plan>> _mockPlanes;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<IConfigurationSection> _mockConfigSection;
    private readonly PagosService _service;
    public PagosServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockLogger = new Mock<ILogger<PagosService>>();
        _mockFactory = new Mock<PaymentGatewayFactory>(BuildFakeStripeGateway());
        _mockUsuariosWebService = new Mock<UsuariosWebService>(_mockDb.Object, Mock.Of<ILogger<UsuariosWebService>>());
        _mockPagos = new Mock<IMongoCollection<Pago>>();
        _mockPlanes = new Mock<IMongoCollection<Plan>>();
        _mockConfig = new Mock<IConfiguration>();
        _mockConfigSection = new Mock<IConfigurationSection>();
        _mockConfigSection.Setup(s => s["SuccessUrl"]).Returns("http://localhost:3000/pago/exito");
        _mockConfigSection.Setup(s => s["CancelUrl"]).Returns("http://localhost:3000/pago/cancelado");
        _mockConfig.Setup(c => c.GetSection("CallbackUrls")).Returns(_mockConfigSection.Object);
        _mockDb.Setup(db => db.Pagos).Returns(_mockPagos.Object);
        _mockDb.Setup(db => db.Planes).Returns(_mockPlanes.Object);
        _service = new PagosService(_mockDb.Object, _mockLogger.Object, _mockFactory.Object, _mockUsuariosWebService.Object, _mockConfig.Object);
    }

    private static StripePaymentGateway BuildFakeStripeGateway()
    {
        var options = new Mock<IOptions<StripeOptions>>();
        options.Setup(o => o.Value).Returns(new StripeOptions());
        return new StripePaymentGateway(options.Object, Mock.Of<ILogger<StripePaymentGateway>>());
    }
    [Fact]
    public async Task CrearSesion_PlanGratis_SaltaPasarela()
    {
        var plan = new Plan { Id = "plan1", Nombre = "Gratis", Precio = 0, PrecioMoneda = "MXN" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);
        _mockPagos.Setup(c => c.InsertOneAsync(
            It.IsAny<Pago>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUsuariosWebService.Setup(u => u.CambiarPlanAsync("user123", "Gratis"))
            .ReturnsAsync(true);
        var result = await _service.CrearSesionAsync("user123", "Gratis");
        result.Should().NotBeNull();
        result!.Estado.Should().Be("completado");
        result.Monto.Should().Be(0);
        result.Gateway.Should().Be("ninguno");
    }
    [Fact]
    public async Task CrearSesion_PlanPagoSinGateway_RetornaNull()
    {
        var plan = new Plan { Id = "plan2", Nombre = "Pro", Precio = 2, PrecioMoneda = "MXN" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);
        var mockGateway = new Mock<IPaymentGateway>();
        mockGateway.Setup(g => g.CreateCheckoutSessionAsync(
                It.IsAny<string>(), It.IsAny<Plan>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new PaymentSessionResult(false, null, null, null, "gateway error"));
        _mockFactory.Setup(f => f.GetGateway("stripe")).Returns(mockGateway.Object);
        var result = await _service.CrearSesionAsync("user123", "Pro");
        result.Should().BeNull();
    }
    [Fact]
    public async Task CrearSesion_PlanPagoConStripe_RetornaPago()
    {
        var plan = new Plan { Id = "plan3", Nombre = "Familiar", Precio = 1, PrecioMoneda = "MXN" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);
        var mockGateway = new Mock<IPaymentGateway>();
        mockGateway.Setup(g => g.CreateCheckoutSessionAsync(
                "user123", plan, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new PaymentSessionResult(true, "cs_test123", "https://checkout.stripe.com/test", "sub_test", null));
        _mockFactory.Setup(f => f.GetGateway("stripe")).Returns(mockGateway.Object);
        _mockPagos.Setup(c => c.InsertOneAsync(
            It.IsAny<Pago>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var result = await _service.CrearSesionAsync("user123", "Familiar", "stripe");
        result.Should().NotBeNull();
        result!.Estado.Should().Be("pendiente");
        result.StripeSessionId.Should().Be("cs_test123");
        result.Gateway.Should().Be("stripe");
        result.Monto.Should().Be(1);
    }
    [Fact]
    public async Task CrearSesion_PlanPagoConMetodoInvalido_RetornaNull()
    {
        var plan = new Plan { Id = "plan4", Nombre = "Pro", Precio = 2, PrecioMoneda = "MXN" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);
        var result = await _service.CrearSesionAsync("user123", "Pro", "mercadopago");
        result.Should().BeNull();
    }
    [Fact]
    public async Task WebhookStripe_PagoCompletado_ConfirmaPago()
    {
        var pago = new Pago { Id = "pago1", StripeSessionId = "cs_test_ok", Estado = "pendiente", UsuarioWebId = "user123", PlanId = "plan1", Gateway = "stripe" };
        var plan = new Plan { Id = "plan1", Nombre = "Familiar", Precio = 1 };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Pago>>(),
                It.IsAny<FilterDefinition<Pago>>(),
                It.IsAny<SortDefinition<Pago>?>()))
            .ReturnsAsync(pago);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);
        _mockPagos.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Pago>>(),
            It.IsAny<UpdateDefinition<Pago>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<UpdateResult>().Object);
        var mockGateway = new Mock<IPaymentGateway>();
        mockGateway.Setup(g => g.VerifyWebhookSignatureAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        mockGateway.Setup(g => g.ParseWebhookEventAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new PaymentWebhookEvent("evt1", "checkout.session.completed", "cs_test_ok", "sub_test", "cus_test", "completado", "plan1"));
        _mockFactory.Setup(f => f.GetGateway("stripe")).Returns(mockGateway.Object);
        var result = await _service.ProcesarWebhookStripeAsync("{}", "test_sig");
        result.Should().BeTrue();
    }
    [Fact]
    public async Task WebhookStripe_Renovacion_RegistraNuevoPago()
    {
        var pagoAnterior = new Pago { Id = "pago1", UsuarioWebId = "user123", PlanId = "plan1", StripeSubscriptionId = "sub_1", Estado = "completado" };
        var plan = new Plan { Id = "plan1", Nombre = "Familiar", Precio = 1, PrecioMoneda = "MXN" };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Pago>>(),
                It.IsAny<FilterDefinition<Pago>>(),
                It.IsAny<SortDefinition<Pago>?>()))
            .ReturnsAsync(pagoAnterior);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);
        _mockPagos.Setup(c => c.InsertOneAsync(
                It.IsAny<Pago>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUsuariosWebService.Setup(u => u.CambiarPlanAsync("user123", "Familiar"))
            .ReturnsAsync(true);

        var mockGateway = new Mock<IPaymentGateway>();
        mockGateway.Setup(g => g.VerifyWebhookSignatureAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        mockGateway.Setup(g => g.ParseWebhookEventAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new PaymentWebhookEvent("evt2", "invoice.paid", null, "sub_1", "cus_1", "renovado", null));
        _mockFactory.Setup(f => f.GetGateway("stripe")).Returns(mockGateway.Object);

        var result = await _service.ProcesarWebhookStripeAsync("{}", "test_sig");

        result.Should().BeTrue();
        _mockPagos.Verify(c => c.InsertOneAsync(
            It.Is<Pago>(p =>
                p.UsuarioWebId == "user123" &&
                p.PlanId == "plan1" &&
                p.Estado == "completado" &&
                p.StripeSubscriptionId == "sub_1" &&
                p.StripeCustomerId == "cus_1" &&
                p.Gateway == "stripe"),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockUsuariosWebService.Verify(u => u.CambiarPlanAsync("user123", "Familiar"), Times.Once);
    }
    [Fact]
    public async Task WebhookStripe_Cancelacion_BajaPlanAGratis()
    {
        var pago = new Pago { Id = "pago1", UsuarioWebId = "user123", PlanId = "plan1", StripeSubscriptionId = "sub_1", Estado = "completado" };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Pago>>(),
                It.IsAny<FilterDefinition<Pago>>(),
                It.IsAny<SortDefinition<Pago>?>()))
            .ReturnsAsync(pago);
        _mockPagos.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<Pago>>(),
                It.IsAny<UpdateDefinition<Pago>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<UpdateResult>().Object);
        _mockUsuariosWebService.Setup(u => u.CambiarPlanAsync("user123", "Gratis"))
            .ReturnsAsync(true);

        var mockGateway = new Mock<IPaymentGateway>();
        mockGateway.Setup(g => g.VerifyWebhookSignatureAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        mockGateway.Setup(g => g.ParseWebhookEventAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new PaymentWebhookEvent("evt3", "customer.subscription.deleted", null, "sub_1", "cus_1", "cancelado", null));
        _mockFactory.Setup(f => f.GetGateway("stripe")).Returns(mockGateway.Object);

        var result = await _service.ProcesarWebhookStripeAsync("{}", "test_sig");

        result.Should().BeTrue();
        _mockUsuariosWebService.Verify(u => u.CambiarPlanAsync("user123", "Gratis"), Times.Once);
        _mockPagos.Verify(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Pago>>(),
            It.IsAny<UpdateDefinition<Pago>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
