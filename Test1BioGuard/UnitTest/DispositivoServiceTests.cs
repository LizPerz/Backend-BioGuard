using MongoDB.Driver;
using Moq;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Config;
using BioGuard.Api.Services;
using BioGuard.Api.Models;
using FluentAssertions;

namespace Test1BioGuard.UnitTest;

public class DispositivoServiceTests
{
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly DispositivoService _service;
    private readonly Mock<IMongoCollection<Dispositivo>> _mockCollection;

    public DispositivoServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockCollection = new Mock<IMongoCollection<Dispositivo>>();
        _mockDb.Setup(db => db.Dispositivos).Returns(_mockCollection.Object);
        var mockLogger = new Mock<ILogger<DispositivoService>>();
        _service = new DispositivoService(_mockDb.Object, mockLogger.Object);
    }

    [Fact]
    public async Task VincularAsync_SinDispositivoPrevio_RetornaDispositivo()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Dispositivo, bool>>>()))
            .ReturnsAsync((Dispositivo?)null);

        _mockCollection.Setup(c => c.InsertOneAsync(
            It.IsAny<Dispositivo>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.VincularAsync("123456789012345678901234", "Galaxy Watch 6", "AA:BB:CC:DD:EE:FF");

        result.Should().NotBeNull();
        result!.NombreDispositivo.Should().Be("Galaxy Watch 6");
        result.MacAddress.Should().Be("AA:BB:CC:DD:EE:FF");
        result.Conectado.Should().BeTrue();
    }

    [Fact]
    public async Task VincularAsync_YaTieneDispositivo_RetornaNull()
    {
        var dispositivoExistente = new Dispositivo
        {
            PacienteId = "123456789012345678901234",
            NombreDispositivo = "Watch anterior"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Dispositivo, bool>>>()))
            .ReturnsAsync(dispositivoExistente);

        var result = await _service.VincularAsync("123456789012345678901234", "Nuevo Watch", "11:22:33:44:55:66");

        result.Should().BeNull();
    }

    [Fact]
    public async Task HeartbeatAsync_DispositivoExiste_RetornaTrue()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockCollection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Dispositivo>>(),
            It.IsAny<UpdateDefinition<Dispositivo>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.HeartbeatAsync("123456789012345678901234");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ObtenerPorPacienteAsync_DispositivoExiste_RetornaDispositivo()
    {
        var dispositivo = new Dispositivo
        {
            Id = "d1", PacienteId = "123456789012345678901234",
            NombreDispositivo = "Galaxy Watch 6", MacAddress = "AA:BB:CC:DD:EE:FF",
            Conectado = true
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Dispositivo, bool>>>()))
            .ReturnsAsync(dispositivo);

        var result = await _service.ObtenerPorPacienteAsync("123456789012345678901234");

        result.Should().NotBeNull();
        result!.NombreDispositivo.Should().Be("Galaxy Watch 6");
    }

    [Fact]
    public async Task ObtenerPorPacienteAsync_SinDispositivo_RetornaNull()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Dispositivo, bool>>>()))
            .ReturnsAsync((Dispositivo?)null);

        var result = await _service.ObtenerPorPacienteAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ActualizarAsync_DispositivoExiste_RetornaTrue()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockCollection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Dispositivo>>(),
            It.IsAny<UpdateDefinition<Dispositivo>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.ActualizarAsync("d1", "Galaxy Watch 7");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ActualizarAsync_DispositivoNoExiste_RetornaFalse()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(0);

        _mockCollection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Dispositivo>>(),
            It.IsAny<UpdateDefinition<Dispositivo>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.ActualizarAsync("nonexistent", "Watch");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EliminarAsync_DispositivoExiste_RetornaTrue()
    {
        var mockResult = new Mock<DeleteResult>();
        mockResult.Setup(r => r.DeletedCount).Returns(1);

        _mockCollection.Setup(c => c.DeleteOneAsync(
            It.IsAny<FilterDefinition<Dispositivo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.EliminarAsync("d1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EliminarAsync_DispositivoNoExiste_RetornaFalse()
    {
        var mockResult = new Mock<DeleteResult>();
        mockResult.Setup(r => r.DeletedCount).Returns(0);

        _mockCollection.Setup(c => c.DeleteOneAsync(
            It.IsAny<FilterDefinition<Dispositivo>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.EliminarAsync("nonexistent");

        result.Should().BeFalse();
    }
}
