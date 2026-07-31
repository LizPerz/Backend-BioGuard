using MongoDB.Driver;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Config;
using BioGuard.Api.Services;
using BioGuard.Api.Models;
using FluentAssertions;
namespace Test1BioGuard.UnitTest;
public class PagosServiceTests
{
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly PagosService _service;
    private readonly Mock<IMongoCollection<Pago>> _mockPagos;
    private readonly Mock<IMongoCollection<Plan>> _mockPlanes;
    private readonly Mock<PaymentGatewayFactory> _mockFactory;
    private readonly Mock<UsuariosWebService> _mockUsuariosWebService;
    private readonly Mock<IConfiguration> _mockConfig;
    public PagosServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockPagos = new Mock<IMongoCollection<Pago>>();
        _mockPlanes = new Mock<IMongoCollection<Plan>>();
        _mockFactory = new Mock<PaymentGatewayFactory>(null!, null!);
        _mockUsuariosWebService = new Mock<UsuariosWebService>(_mockDb.Object, Mock.Of<ILogger<UsuariosWebService>>());
        _mockConfig = new Mock<IConfiguration>();
        var configSection = new Mock<IConfigurationSection>();
        configSection.Setup(s => s["SuccessUrl"]).Returns("http://localhost:3000/pago/exito");
        configSection.Setup(s => s["CancelUrl"]).Returns("http://localhost:3000/pago/cancelado");
        _mockConfig.Setup(c => c.GetSection("CallbackUrls")).Returns(configSection.Object);
        _mockDb.Setup(db => db.Pagos).Returns(_mockPagos.Object);
        _mockDb.Setup(db => db.Planes).Returns(_mockPlanes.Object);
        var mockLogger = new Mock<ILogger<PagosService>>();
        _service = new PagosService(_mockDb.Object, mockLogger.Object, _mockFactory.Object, _mockUsuariosWebService.Object, _mockConfig.Object);
    }
    [Fact]
    public async Task CrearSesionAsync_PlanGratis_RetornaPagoCompletado()
    {
        var plan = new Plan { Id = "plan123", Nombre = "Gratis", Precio = 0, PrecioMoneda = "MXN" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockPlanes.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);
        _mockPagos.Setup(c => c.InsertOneAsync(
            It.IsAny<Pago>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUsuariosWebService.Setup(u => u.CambiarPlanAsync("user123", "Gratis")).ReturnsAsync(true);
        var result = await _service.CrearSesionAsync("user123", "Gratis");
        result.Should().NotBeNull();
        result!.Monto.Should().Be(0);
        result.Moneda.Should().Be("MXN");
        result.Estado.Should().Be("completado");
        result.Gateway.Should().Be("ninguno");
    }
    [Fact]
    public async Task CrearSesionAsync_PlanPagoConStripe_RetornaPago()
    {
        var plan = new Plan { Id = "plan123", Nombre = "Familiar", Precio = 1, PrecioMoneda = "MXN" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockPlanes.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);
        var mockGateway = new Mock<IPaymentGateway>();
        mockGateway.Setup(g => g.CreateCheckoutSessionAsync("user123", plan, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new PaymentSessionResult(true, "cs_test_stripe", "https://checkout.stripe.com/test", "sub_test", null));
        _mockFactory.Setup(f => f.GetGateway("stripe")).Returns(mockGateway.Object);
        _mockPagos.Setup(c => c.InsertOneAsync(
            It.IsAny<Pago>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var result = await _service.CrearSesionAsync("user123", "Familiar", "stripe");
        result.Should().NotBeNull();
        result!.Monto.Should().Be(1);
        result.Moneda.Should().Be("MXN");
        result.Estado.Should().Be("pendiente");
        result.StripeSessionId.Should().Be("cs_test_stripe");
        result.Gateway.Should().Be("stripe");
    }
    [Fact]
    public async Task CrearSesionAsync_PlanNoExiste_RetornaNull()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockPlanes.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync((Plan?)null);
        var result = await _service.CrearSesionAsync("user123", "PlanInexistente");
        result.Should().BeNull();
    }
    [Fact]
    public async Task ObtenerHistorialAsync_ConPagos_RetornaLista()
    {
        var pagos = new List<Pago>
        {
            new() { Monto = 1m, Estado = "completado", FechaPago = DateTime.UtcNow },
            new() { Monto = 2m, Estado = "completado", FechaPago = DateTime.UtcNow.AddDays(-30) }
        };
        _mockDb.Setup(db => db.FindToListAsync(
                _mockPagos.Object,
                It.IsAny<FilterDefinition<Pago>>(),
                It.IsAny<SortDefinition<Pago>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(pagos);
        var result = await _service.ObtenerHistorialAsync("user123");
        result.Should().NotBeEmpty();
        result.Should().HaveCount(2);
    }
    [Fact]
    public async Task CancelarAsync_PagoActivo_RetornaTrue()
    {
        var pagoActivo = new Pago
        {
            Id = "pago123", Estado = "completado", UsuarioWebId = "user123",
            StripeSessionId = "cs_123", Gateway = "stripe"
        };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockPagos.Object,
                It.IsAny<FilterDefinition<Pago>>(),
                It.IsAny<SortDefinition<Pago>>()))
            .ReturnsAsync(pagoActivo);
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockPagos.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Pago>>(),
            It.IsAny<UpdateDefinition<Pago>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);
        var mockGateway = new Mock<IPaymentGateway>();
        mockGateway.Setup(g => g.CancelSubscriptionAsync("cs_123")).ReturnsAsync(true);
        _mockFactory.Setup(f => f.GetGateway("stripe")).Returns(mockGateway.Object);
        var result = await _service.CancelarAsync("user123");
        result.Should().BeTrue();
    }
}
