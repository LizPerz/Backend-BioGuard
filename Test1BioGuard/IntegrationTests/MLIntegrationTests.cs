using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Controllers;
using BioGuard.Api.Models;

namespace Test1BioGuard.IntegrationTests;

public class MLIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<PrediccionMl>> _mockPredicciones;
    private readonly Mock<IMongoCollection<ModeloMl>> _mockModelos;

    public MLIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;
        _mockPredicciones = new Mock<IMongoCollection<PrediccionMl>>();
        _mockModelos = new Mock<IMongoCollection<ModeloMl>>();
        _mockDb.Setup(db => db.PrediccionesMl).Returns(_mockPredicciones.Object);
        _mockDb.Setup(db => db.ModelosMl).Returns(_mockModelos.Object);
    }

    [Fact]
    public async Task ObtenerPredicciones_ConPredicciones_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var predicciones = new List<PrediccionMl>
        {
            new()
            {
                Id = "pred1", PacienteId = pacienteId, ProbabilidadPico = 0.75,
                NivelRiesgo = "Pre-Pico", Recomendacion = "Hidratarse",
                FechaPrediccion = DateTime.UtcNow, HorasEstimadas = 4,
                ModeloVersion = "v1.0"
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<PrediccionMl>>(),
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(predicciones);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/ML/predicciones/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ObtenerPredicciones_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/ML/predicciones/pac123");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PrediccionActual_SinPrediccion_RetornaSinDatos()
    {
        var pacienteId = "123456789012345678901234";

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<PrediccionMl>>(),
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>>()))
            .ReturnsAsync((PrediccionMl?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/ML/predicciones/{pacienteId}/actual");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Sin predicción activa");
    }

    [Fact]
    public async Task ListarModelos_ConModelos_Retorna200()
    {
        var modelos = new List<ModeloMl>
        {
            new()
            {
                Id = "mod1", Version = "v1.0", Accuracy = 0.92,
                Precision = 0.89, Recall = 0.95, F1Score = 0.92,
                Activo = true, TotalMuestras = 10000,
                FechaEntrenamiento = DateTime.UtcNow, Descripcion = "Modelo inicial"
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<ModeloMl>>(),
                It.IsAny<FilterDefinition<ModeloMl>>(),
                It.IsAny<SortDefinition<ModeloMl>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(modelos);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/ML/modelos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task MetricasModelo_ModeloExiste_Retorna200()
    {
        var modelo = new ModeloMl
        {
            Id = "mod1", Version = "v1.0", Accuracy = 0.92,
            Precision = 0.89, Recall = 0.95, F1Score = 0.92,
            TotalMuestras = 10000, FechaEntrenamiento = DateTime.UtcNow
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<ModeloMl>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<ModeloMl, bool>>>()))
            .ReturnsAsync(modelo);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/ML/metricas/mod1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("version").GetString().Should().Be("v1.0");
    }

    [Fact]
    public async Task MetricasModelo_ModeloNoExiste_Retorna404()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<ModeloMl>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<ModeloMl, bool>>>()))
            .ReturnsAsync((ModeloMl?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/ML/metricas/invalid_id");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Recomendaciones_ConPrediccion_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var prediccion = new PrediccionMl
        {
            Id = "pred1", PacienteId = pacienteId, ProbabilidadPico = 0.85,
            NivelRiesgo = "Pre-Pico", Recomendacion = "Hidratarse constantemente",
            FechaPrediccion = DateTime.UtcNow, FechaExpiracion = DateTime.UtcNow.AddHours(2),
            HorasEstimadas = 3, ModeloVersion = "v1.0"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<PrediccionMl>>(),
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>>()))
            .ReturnsAsync(prediccion);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/ML/recomendaciones/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("recomendaciones");
        arr.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        arr[0].GetString().Should().Be("Hidratarse constantemente");
    }

    [Fact]
    public async Task Recomendaciones_SinPrediccion_Retorna200_Vacia()
    {
        var pacienteId = "123456789012345678901234";

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<PrediccionMl>>(),
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>>()))
            .ReturnsAsync((PrediccionMl?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/ML/recomendaciones/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("recomendaciones").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task EntrenarModelo_DatosValidos_Retorna200()
    {
        _mockModelos.Setup(c => c.InsertOneAsync(
                It.IsAny<ModeloMl>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<ModeloMl, InsertOneOptions, CancellationToken>((m, _, _) =>
            {
                m.Id = "newModId123";
            })
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new EntrenarModeloRequest("v2.0", "Re-entrenamiento con nuevos datos");
        var response = await _client.PostAsJsonAsync("/api/ML/entrenar", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("modeloId").GetString().Should().Be("newModId123");
        doc.RootElement.GetProperty("message").GetString().Should().Be("Entrenamiento iniciado");
    }

    // ── POST /api/ML/reentrenar ─────────────────────────────

    [Fact]
    public async Task ReentrenarModelo_DatosValidos_Retorna200()
    {
        var modeloActivo = new ModeloMl
        {
            Id = "mod1", Version = "v1.0", Accuracy = 0.92,
            Precision = 0.89, Recall = 0.95, F1Score = 0.92,
            Activo = true, TotalMuestras = 10000
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<ModeloMl>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<ModeloMl, bool>>>()))
            .ReturnsAsync(modeloActivo);

        _mockModelos.Setup(c => c.InsertOneAsync(
                It.IsAny<ModeloMl>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<ModeloMl, InsertOneOptions, CancellationToken>((m, _, _) =>
            {
                m.Id = "reentId456";
            })
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new EntrenarModeloRequest("v2.1", "Re-entrenamiento incremental");
        var response = await _client.PostAsJsonAsync("/api/ML/reentrenar", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("modeloId").GetString().Should().Be("reentId456");
        doc.RootElement.GetProperty("message").GetString().Should().Be("Re-entrenamiento iniciado");
    }

    // ── POST /api/ML/diagnosticar ───────────────────────────

    [Fact]
    public async Task Diagnosticar_ConPredicciones_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var predicciones = new List<PrediccionMl>
        {
            new()
            {
                Id = "pred1", PacienteId = pacienteId, ProbabilidadPico = 0.75,
                NivelRiesgo = "Pre-Pico", Recomendacion = "Hidratarse",
                FechaPrediccion = DateTime.UtcNow, HorasEstimadas = 4,
                ModeloVersion = "v1.0"
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<PrediccionMl>>(),
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(predicciones);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new DiagnosticarRequest(pacienteId);
        var response = await _client.PostAsJsonAsync("/api/ML/diagnosticar", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("nivelRiesgo").GetString().Should().Be("Pre-Pico");
        doc.RootElement.GetProperty("probabilidad").GetDouble().Should().Be(0.75);
    }

    [Fact]
    public async Task Diagnosticar_SinPredicciones_RetornaSinDatos()
    {
        var pacienteId = "123456789012345678901234";

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<PrediccionMl>>(),
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<PrediccionMl>());

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new DiagnosticarRequest(pacienteId);
        var response = await _client.PostAsJsonAsync("/api/ML/diagnosticar", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Sin datos suficientes para diagnóstico");
    }

    [Fact]
    public async Task Diagnosticar_SinToken_Retorna401()
    {
        var request = new DiagnosticarRequest("123456789012345678901234");
        var response = await _client.PostAsJsonAsync("/api/ML/diagnosticar", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ObtenerRecomendaciones_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/ML/recomendaciones/123456789012345678901234");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ObtenerModelos_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/ML/modelos");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Entrenar_SinToken_Retorna401()
    {
        var response = await _client.PostAsJsonAsync("/api/ML/entrenar", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
