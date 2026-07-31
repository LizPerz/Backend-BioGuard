using MongoDB.Driver;
using Moq;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Config;
using BioGuard.Api.Services;
using BioGuard.Api.Models;
using FluentAssertions;

namespace Test1BioGuard.UnitTest;

public class AlertaServiceTests
{
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly AlertaService _service;
    private readonly Mock<IMongoCollection<Alerta>> _mockCollection;

    public AlertaServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockCollection = new Mock<IMongoCollection<Alerta>>();
        _mockDb.Setup(db => db.Alertas).Returns(_mockCollection.Object);
        var mockLogger = new Mock<ILogger<AlertaService>>();
        _service = new AlertaService(_mockDb.Object, mockLogger.Object);
    }

    [Fact]
    public async Task ObtenerPorPacienteAsync_ConAlertas_RetornaLista()
    {
        var alertas = new List<Alerta>
        {
            new() { Id = "a1", PacienteId = "pac1", Tipo = "glucosa", Nivel = "critico", Titulo = "Alerta alta", Mensaje = "Glucosa elevada", Atendida = false },
            new() { Id = "a2", PacienteId = "pac1", Tipo = "pulso", Nivel = "medio", Titulo = "Pulso alto", Mensaje = "BPM elevado", Atendida = true }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                _mockCollection.Object,
                It.IsAny<FilterDefinition<Alerta>>(),
                It.IsAny<SortDefinition<Alerta>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(alertas);

        var result = await _service.ObtenerPorPacienteAsync("pac1");

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ObtenerPendientesAsync_ConPendientes_RetornaLista()
    {
        var alertas = new List<Alerta>
        {
            new() { Id = "a1", PacienteId = "pac1", Titulo = "Pendiente", Atendida = false }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                _mockCollection.Object,
                It.IsAny<FilterDefinition<Alerta>>(),
                It.IsAny<SortDefinition<Alerta>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(alertas);

        var result = await _service.ObtenerPendientesAsync("pac1");

        result.Should().HaveCount(1);
        result[0].Atendida.Should().BeFalse();
    }

    [Fact]
    public async Task ObtenerPorIdAsync_AlertaExiste_RetornaAlerta()
    {
        var alerta = new Alerta
        {
            Id = "a1", PacienteId = "pac1", Tipo = "glucosa",
            Nivel = "critico", Titulo = "Alerta", Mensaje = "Mensaje"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Alerta, bool>>>()))
            .ReturnsAsync(alerta);

        var result = await _service.ObtenerPorIdAsync("a1");

        result.Should().NotBeNull();
        result!.Tipo.Should().Be("glucosa");
    }

    [Fact]
    public async Task CrearAsync_DatosValidos_RetornaAlerta()
    {
        _mockCollection.Setup(c => c.InsertOneAsync(
            It.IsAny<Alerta>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sensorData = new SensorData { PulsoBpm = 120, TemperaturaC = 38.5 };
        var result = await _service.CrearAsync("pac1", "glucosa", "critico", "Glucosa alta", "Nivel peligroso", sensorData);

        result.Should().NotBeNull();
        result.Tipo.Should().Be("glucosa");
        result.Nivel.Should().Be("critico");
        result.Titulo.Should().Be("Glucosa alta");
        result.Atendida.Should().BeFalse();
        result.SensorData!.PulsoBpm.Should().Be(120);
    }

    [Fact]
    public async Task ResolverAsync_AlertaExiste_RetornaTrue()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockCollection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Alerta>>(),
            It.IsAny<UpdateDefinition<Alerta>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.ResolverAsync("a1", "cuidador1", "Medicina administrada");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EliminarAsync_AlertaExiste_RetornaTrue()
    {
        var mockResult = new Mock<DeleteResult>();
        mockResult.Setup(r => r.DeletedCount).Returns(1);

        _mockCollection.Setup(c => c.DeleteOneAsync(
            It.IsAny<FilterDefinition<Alerta>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.EliminarAsync("a1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EliminarPorPacienteAsync_DatosValidos_RetornaTrue()
    {
        var mockResult = new Mock<DeleteResult>();
        mockResult.Setup(r => r.DeletedCount).Returns(5);

        _mockDb.Setup(db => db.DeleteManyAsync(
                It.IsAny<IMongoCollection<Alerta>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Alerta, bool>>>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.EliminarPorPacienteAsync("pac1");

        result.Should().BeTrue();
    }
}
