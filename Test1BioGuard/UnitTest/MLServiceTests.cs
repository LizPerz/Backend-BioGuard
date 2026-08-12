using MongoDB.Driver;
using Moq;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Config;
using BioGuard.Api.Services;
using BioGuard.Api.Models;
using FluentAssertions;

namespace Test1BioGuard.UnitTest;

public class MLServiceTests
{
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly MLService _service;
    private readonly Mock<IMongoCollection<PrediccionMl>> _mockPredicciones;

    public MLServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockPredicciones = new Mock<IMongoCollection<PrediccionMl>>();

        _mockDb.Setup(db => db.PrediccionesMl).Returns(_mockPredicciones.Object);

        var mockLogger = new Mock<ILogger<MLService>>();
        _service = new MLService(_mockDb.Object, mockLogger.Object);
    }

    [Fact]
    public async Task ObtenerPrediccionActualAsync_ConPrediccionActiva_RetornaPrediccion()
    {
        var prediccion = new PrediccionMl
        {
            Id = "pred123",
            PacienteId = "123456789012345678901234",
            ProbabilidadPico = 0.75,
            NivelRiesgo = "Pre-Pico",
            Recomendacion = "Mantener hidratación",
            FechaPrediccion = DateTime.UtcNow,
            FechaExpiracion = DateTime.UtcNow.AddHours(2)
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockPredicciones.Object,
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>>()))
            .ReturnsAsync(prediccion);

        var result = await _service.ObtenerPrediccionActualAsync("123456789012345678901234");

        result.Should().NotBeNull();
        result!.ProbabilidadPico.Should().Be(0.75);
        result.NivelRiesgo.Should().Be("Pre-Pico");
    }

    [Fact]
    public async Task ObtenerPrediccionActualAsync_SinPrediccionActiva_RetornaNull()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockPredicciones.Object,
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>>()))
            .ReturnsAsync((PrediccionMl?)null);

        var result = await _service.ObtenerPrediccionActualAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerRecomendacionesAsync_NivelCritico_RetornaRecomendacionesEspeciales()
    {
        var prediccion = new PrediccionMl
        {
            PacienteId = "123456789012345678901234",
            NivelRiesgo = "Critico",
            Recomendacion = "Evitar azúcares",
            FechaExpiracion = DateTime.UtcNow.AddHours(2)
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockPredicciones.Object,
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>>()))
            .ReturnsAsync(prediccion);

        var result = await _service.ObtenerRecomendacionesAsync("123456789012345678901234");

        result.Should().NotBeEmpty();
        result.Should().Contain(r => r.Contains("Evitar azúcares"));
        result.Should().Contain(r => r.Contains("cuidador"));
        result.Should().Contain(r => r.Contains("glucosa"));
    }

    [Fact]
    public async Task GuardarPrediccionAsync_EntidadValida_GuardaYRetornaEntidad()
    {
        var entidad = new PrediccionMl
        {
            PacienteId = "123456789012345678901234",
            ProbabilidadPico = 0.75,
            NivelRiesgo = "Pre-Pico",
            HorasEstimadas = 2,
            Recomendacion = "Mantener hidratación",
            ModeloVersion = "pico-v1.0",
            Imc = 26.12,
            Z = -1.6,
            PPico = 0.17,
            CasoClinico = "Vigilancia",
            AccionAutomatizada = "Observación",
            FechaPrediccion = DateTime.UtcNow,
            FechaExpiracion = DateTime.UtcNow.AddHours(2)
        };

        _mockPredicciones.Setup(c => c.InsertOneAsync(
                It.IsAny<PrediccionMl>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.GuardarPrediccionAsync(entidad);

        result.Should().NotBeNull();
        result.PacienteId.Should().Be("123456789012345678901234");
        result.ProbabilidadPico.Should().Be(0.75);
        result.NivelRiesgo.Should().Be("Pre-Pico");
        result.Imc.Should().Be(26.12);
        result.PPico.Should().Be(0.17);
        result.CasoClinico.Should().Be("Vigilancia");
    }

    [Fact]
    public async Task GuardarPrediccionAsync_SinFechaPrediccion_AsignaFechaActual()
    {
        var entidad = new PrediccionMl
        {
            PacienteId = "123456789012345678901234",
            ProbabilidadPico = 0.4,
            NivelRiesgo = "Normal",
            FechaPrediccion = default
        };

        _mockPredicciones.Setup(c => c.InsertOneAsync(
                It.IsAny<PrediccionMl>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.GuardarPrediccionAsync(entidad);

        result.FechaPrediccion.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ObtenerPrediccionesAsync_ConPredicciones_RetornaLista()
    {
        var predicciones = new List<PrediccionMl>
        {
            new() { Id = "p1", PacienteId = "123456789012345678901234", ProbabilidadPico = 0.75, NivelRiesgo = "Pre-Pico", FechaPrediccion = DateTime.UtcNow },
            new() { Id = "p2", PacienteId = "123456789012345678901234", ProbabilidadPico = 0.4, NivelRiesgo = "Normal", FechaPrediccion = DateTime.UtcNow.AddHours(-1) }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                _mockPredicciones.Object,
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(predicciones);

        var result = await _service.ObtenerPrediccionesAsync("123456789012345678901234");

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ObtenerPrediccionesAsync_SinPredicciones_RetornaListaVacia()
    {
        _mockDb.Setup(db => db.FindToListAsync(
                _mockPredicciones.Object,
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<PrediccionMl>());

        var result = await _service.ObtenerPrediccionesAsync("nonexistent");

        result.Should().BeEmpty();
    }
}
