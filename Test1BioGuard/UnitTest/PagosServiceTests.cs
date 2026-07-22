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
    private readonly Mock<IMongoCollection<Plan>> _mockPlanes;

    public PagosServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockPagos = new Mock<IMongoCollection<Pago>>();
        _mockPlanes = new Mock<IMongoCollection<Plan>>();

        _mockDb.Setup(db => db.Pagos).Returns(_mockPagos.Object);
        _mockDb.Setup(db => db.Planes).Returns(_mockPlanes.Object);

        var mockLogger = new Mock<ILogger<PagosService>>();
        _service = new PagosService(_mockDb.Object, mockLogger.Object);
    }

    [Fact]
    public async Task CrearSesionAsync_PlanValido_RetornaPago()
    {
        var plan = new Plan
        {
            Id = "plan123",
            Nombre = "Familiar",
            Precio = 9.99m,
            PrecioMoneda = "USD"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockPlanes.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        _mockPagos.Setup(c => c.InsertOneAsync(
            It.IsAny<Pago>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CrearSesionAsync("user123", "Familiar");

        result.Should().NotBeNull();
        result!.Monto.Should().Be(9.99m);
        result.Moneda.Should().Be("USD");
        result.Estado.Should().Be("pendiente");
        result.StripeSessionId.Should().StartWith("cs_");
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
            new() { Monto = 9.99m, Estado = "completado", FechaPago = DateTime.UtcNow },
            new() { Monto = 19.99m, Estado = "completado", FechaPago = DateTime.UtcNow.AddDays(-30) }
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
            UsuarioWebId = "user123"
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

        var result = await _service.CancelarAsync("user123");

        result.Should().BeTrue();
    }
}
