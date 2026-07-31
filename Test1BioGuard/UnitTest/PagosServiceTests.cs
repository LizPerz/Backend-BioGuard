using MongoDB.Driver;
using Moq;
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
    private readonly Mock<IMongoCollection<EventoProcesado>> _mockEventosProcesados;
    private readonly Mock<IMongoCollection<Plan>> _mockPlanes;
    private readonly Mock<IMongoCollection<UsuarioWeb>> _mockUsuarios;
    private readonly Mock<IPaymentGateway> _mockGateway;

    public PagosServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockPagos = new Mock<IMongoCollection<Pago>>();
        _mockEventosProcesados = new Mock<IMongoCollection<EventoProcesado>>();
        _mockPlanes = new Mock<IMongoCollection<Plan>>();
        _mockUsuarios = new Mock<IMongoCollection<UsuarioWeb>>();
        _mockGateway = new Mock<IPaymentGateway>();

        _mockDb.Setup(db => db.Pagos).Returns(_mockPagos.Object);
        _mockDb.Setup(db => db.EventosProcesados).Returns(_mockEventosProcesados.Object);
        _mockDb.Setup(db => db.Planes).Returns(_mockPlanes.Object);
        _mockDb.Setup(db => db.UsuariosWeb).Returns(_mockUsuarios.Object);

        var mockLogger = new Mock<ILogger<PagosService>>();
        _service = new PagosService(_mockDb.Object, mockLogger.Object);
    }

    [Fact]
    public async Task CrearSesionAsync_GatewayResultValido_RetornaPago()
    {
        var plan = new Plan
        {
            Id = "plan123",
            Nombre = "Care",
            Precio = 129m,
            PrecioMoneda = "MXN"
        };

        var gatewayResult = new PaymentSessionResult(true, "cs_test_123", "https://checkout.stripe.com/pay/cs_test_123", "sub_456", null);

        _mockPagos.Setup(c => c.InsertOneAsync(
            It.IsAny<Pago>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CrearSesionAsync("user123", plan, gatewayResult, "stripe");

        result.Should().NotBeNull();
        result!.Monto.Should().Be(129m);
        result.Moneda.Should().Be("MXN");
        result.Estado.Should().Be("pendiente");
        result.StripeSessionId.Should().Be("cs_test_123");
        result.SesionUrl.Should().Be("https://checkout.stripe.com/pay/cs_test_123");
        result.StripeSubscriptionId.Should().Be("sub_456");
        result.MetodoPago.Should().Be("stripe");
    }

    [Fact]
    public async Task ObtenerHistorialAsync_ConPagos_RetornaLista()
    {
        var pagos = new List<Pago>
        {
            new() { Monto = 129m, Estado = "completado", FechaPago = DateTime.UtcNow },
            new() { Monto = 69m, Estado = "completado", FechaPago = DateTime.UtcNow.AddDays(-30) }
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
            Id = "pago123",
            Estado = "completado",
            UsuarioWebId = "user123",
            StripeSubscriptionId = "sub_456"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockPagos.Object,
                It.IsAny<FilterDefinition<Pago>>(),
                It.IsAny<SortDefinition<Pago>>()))
            .ReturnsAsync(pagoActivo);

        _mockGateway.Setup(g => g.CancelSubscriptionAsync("sub_456"))
            .ReturnsAsync(true);

        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockPagos.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Pago>>(),
            It.IsAny<UpdateDefinition<Pago>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.CancelarAsync("user123", _mockGateway.Object);

        result.Should().BeTrue();
        _mockGateway.Verify(g => g.CancelSubscriptionAsync("sub_456"), Times.Once);
    }

    [Fact]
    public async Task CancelarAsync_SinPagoActivo_RetornaFalse()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockPagos.Object,
                It.IsAny<FilterDefinition<Pago>>(),
                It.IsAny<SortDefinition<Pago>>()))
            .ReturnsAsync((Pago?)null);

        var result = await _service.CancelarAsync("user123", _mockGateway.Object);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ActualizarPagoCompletadoAsync_ActualizaEstado()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockPagos.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Pago>>(),
            It.IsAny<UpdateDefinition<Pago>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.ActualizarPagoCompletadoAsync("cs_test_123", "cus_456", "sub_789");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarPlanUsuarioAsync_AsignaPlan()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockUsuarios.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        await _service.ActualizarPlanUsuarioAsync("user123", "plan456");
    }

    [Fact]
    public async Task EventoYaProcesadoAsync_EventoExistente_RetornaTrue()
    {
        var pagoExistente = new Pago { EventoId = "evt_123" };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockEventosProcesados.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<EventoProcesado, bool>>>()))
            .ReturnsAsync(new EventoProcesado { Id = "evt_123" });

        var result = await _service.EventoYaProcesadoAsync("evt_123");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EventoYaProcesadoAsync_EventoNuevo_RetornaFalse()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockEventosProcesados.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<EventoProcesado, bool>>>()))
            .ReturnsAsync((EventoProcesado?)null);

        var result = await _service.EventoYaProcesadoAsync("evt_nuevo");

        result.Should().BeFalse();
    }
}
