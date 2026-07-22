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
    private readonly Mock<IMongoCollection<ModeloMl>> _mockModelos;

    public MLServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockPredicciones = new Mock<IMongoCollection<PrediccionMl>>();
        _mockModelos = new Mock<IMongoCollection<ModeloMl>>();

        _mockDb.Setup(db => db.PrediccionesMl).Returns(_mockPredicciones.Object);
        _mockDb.Setup(db => db.ModelosMl).Returns(_mockModelos.Object);

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

    [Fact]
    public async Task ObtenerModelosAsync_ConModelos_RetornaLista()
    {
        var modelos = new List<ModeloMl>
        {
            new() { Id = "m1", Version = "1.0.0", Descripcion = "Modelo v1", Activo = true, FechaEntrenamiento = DateTime.UtcNow },
            new() { Id = "m2", Version = "2.0.0", Descripcion = "Modelo v2", Activo = false, FechaEntrenamiento = DateTime.UtcNow.AddDays(-1) }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                _mockModelos.Object,
                It.IsAny<FilterDefinition<ModeloMl>>(),
                It.IsAny<SortDefinition<ModeloMl>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(modelos);

        var result = await _service.ObtenerModelosAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ObtenerModeloActivoAsync_ConModeloActivo_RetornaModelo()
    {
        var modelo = new ModeloMl
        {
            Id = "m1", Version = "1.0.0", Activo = true, Accuracy = 0.92
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockModelos.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<ModeloMl, bool>>>()))
            .ReturnsAsync(modelo);

        var result = await _service.ObtenerModeloActivoAsync();

        result.Should().NotBeNull();
        result!.Activo.Should().BeTrue();
        result.Accuracy.Should().Be(0.92);
    }

    [Fact]
    public async Task ObtenerModeloActivoAsync_SinModeloActivo_RetornaNull()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockModelos.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<ModeloMl, bool>>>()))
            .ReturnsAsync((ModeloMl?)null);

        var result = await _service.ObtenerModeloActivoAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task ObtenerMetricasAsync_ModeloExiste_RetornaModelo()
    {
        var modelo = new ModeloMl
        {
            Id = "m1", Version = "1.0.0", Accuracy = 0.92, Activo = true
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockModelos.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<ModeloMl, bool>>>()))
            .ReturnsAsync(modelo);

        var result = await _service.ObtenerMetricasAsync("m1");

        result.Should().NotBeNull();
        result!.Accuracy.Should().Be(0.92);
    }

    [Fact]
    public async Task ObtenerMetricasAsync_ModeloNoExiste_RetornaNull()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockModelos.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<ModeloMl, bool>>>()))
            .ReturnsAsync((ModeloMl?)null);

        var result = await _service.ObtenerMetricasAsync("nonexistent");

        result.Should().BeNull();
    }
}
