using MongoDB.Driver;
using Moq;
using BioGuard.Api.Config;
using BioGuard.Api.Services;
using BioGuard.Api.Models;
using FluentAssertions;

namespace Test1BioGuard.UnitTest;

public class SensorServiceTests
{
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly SensorService _service;
    private readonly Mock<IMongoCollection<LecturaSensor>> _mockLecturas;
    private readonly Mock<IMongoCollection<EventoMetabolico>> _mockEventos;
    private readonly Mock<IMongoCollection<TrackingGps>> _mockTracking;

    public SensorServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockLecturas = new Mock<IMongoCollection<LecturaSensor>>();
        _mockEventos = new Mock<IMongoCollection<EventoMetabolico>>();
        _mockTracking = new Mock<IMongoCollection<TrackingGps>>();

        _mockDb.Setup(db => db.LecturasSensores).Returns(_mockLecturas.Object);
        _mockDb.Setup(db => db.EventosMetabolicos).Returns(_mockEventos.Object);
        _mockDb.Setup(db => db.TrackingGps).Returns(_mockTracking.Object);

        _service = new SensorService(_mockDb.Object);
    }

    [Fact]
    public async Task InsertarLecturaAsync_DatosValidos_RetornaLectura()
    {
        _mockLecturas.Setup(c => c.InsertOneAsync(
            It.IsAny<LecturaSensor>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.InsertarLecturaAsync(
            "123456789012345678901234", "AA:BB:CC:DD:EE:FF", 72, 36.5, 2.5, 0.3);

        result.Should().NotBeNull();
        result.PulsoBpm.Should().Be(72);
        result.TemperaturaC.Should().Be(36.5);
        result.SudoracionGsr.Should().Be(2.5);
        result.Meta.PacienteId.Should().Be("123456789012345678901234");
        result.Meta.DispositivoMac.Should().Be("AA:BB:CC:DD:EE:FF");
    }

    [Fact]
    public async Task CrearEventoAsync_NivelCritico_RetornaEvento()
    {
        _mockEventos.Setup(c => c.InsertOneAsync(
            It.IsAny<EventoMetabolico>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CrearEventoAsync("123456789012345678901234", 0.95, "Critico", "Pico de glucosa detectado");

        result.Should().NotBeNull();
        result.NivelRiesgo.Should().Be("Critico");
        result.ProbabilidadMl.Should().Be(0.95);
        result.Descripcion.Should().Be("Pico de glucosa detectado");
        result.Atendida.Should().BeFalse();
    }

    [Fact]
    public async Task AtenderEventoAsync_EventoExiste_RetornaTrue()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockEventos.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<EventoMetabolico>>(),
            It.IsAny<UpdateDefinition<EventoMetabolico>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.AtenderEventoAsync("123456789012345678901234", "cuidador123");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task InsertarTrackingAsync_Emergencia_GuardaCorrectamente()
    {
        _mockTracking.Setup(c => c.InsertOneAsync(
            It.IsAny<TrackingGps>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.InsertarTrackingAsync("123456789012345678901234", "AA:BB:CC:DD:EE:FF", -99.1332, 19.4326, true);

        _mockTracking.Verify(c => c.InsertOneAsync(
            It.Is<TrackingGps>(t =>
                t.Meta.PacienteId == "123456789012345678901234" &&
                t.Ubicacion.Coordinates[0] == -99.1332 &&
                t.Ubicacion.Coordinates[1] == 19.4326 &&
                t.EsEmergencia == true),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ObtenerLecturasAsync_ConDatos_RetornaLista()
    {
        var lecturas = new List<LecturaSensor>
        {
            new() { PulsoBpm = 72, Timestamp = DateTime.UtcNow },
            new() { PulsoBpm = 85, Timestamp = DateTime.UtcNow.AddMinutes(-10) }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                _mockLecturas.Object,
                It.IsAny<FilterDefinition<LecturaSensor>>(),
                It.IsAny<SortDefinition<LecturaSensor>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(lecturas);

        var result = await _service.ObtenerLecturasAsync("123456789012345678901234", 100);

        result.Should().NotBeEmpty();
        result.Should().HaveCount(2);
    }
}
