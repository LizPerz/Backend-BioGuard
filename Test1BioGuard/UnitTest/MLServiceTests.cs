using MongoDB.Driver;
using Moq;
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
    private readonly Mock<IMongoCollection<ModeloMl>> _mockModelos;

    public MLServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockPredicciones = new Mock<IMongoCollection<PrediccionMl>>();
        _mockModelos = new Mock<IMongoCollection<ModeloMl>>();

        _mockDb.Setup(db => db.PrediccionesMl).Returns(_mockPredicciones.Object);
        _mockDb.Setup(db => db.ModelosMl).Returns(_mockModelos.Object);

        _service = new MLService(_mockDb.Object);
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
    public async Task CrearModeloAsync_ModeloValido_RetornaModelo()
    {
        var modelo = new ModeloMl
        {
            Version = "1.0.0",
            Descripcion = "Modelo inicial"
        };

        _mockModelos.Setup(c => c.InsertOneAsync(
            It.IsAny<ModeloMl>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CrearModeloAsync(modelo);

        result.Should().NotBeNull();
        result.Version.Should().Be("1.0.0");
        result.Accuracy.Should().Be(0);
        result.Activo.Should().BeFalse();
    }
}
