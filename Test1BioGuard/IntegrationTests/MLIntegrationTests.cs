using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using BioGuard.Api.Controllers;
using BioGuard.Api.DTOs;

namespace Test1BioGuard.IntegrationTests;

public class MLIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<PrediccionMl>> _mockPredicciones;
    private readonly Mock<IMongoCollection<ModeloMl>> _mockModelos;
    private readonly Mock<IMongoCollection<Paciente>> _mockPacientes;
    private readonly Mock<IMongoCollection<Cuidador>> _mockCuidadores;
    private readonly Mock<IMongoCollection<LecturaSensor>> _mockLecturas;

    public MLIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;

        _mockPredicciones = new Mock<IMongoCollection<PrediccionMl>>();
        _mockModelos = new Mock<IMongoCollection<ModeloMl>>();
        _mockPacientes = new Mock<IMongoCollection<Paciente>>();
        _mockCuidadores = new Mock<IMongoCollection<Cuidador>>();
        _mockLecturas = new Mock<IMongoCollection<LecturaSensor>>();

        _mockDb.Setup(db => db.PrediccionesMl).Returns(_mockPredicciones.Object);
        _mockDb.Setup(db => db.ModelosMl).Returns(_mockModelos.Object);
        _mockDb.Setup(db => db.Pacientes).Returns(_mockPacientes.Object);
        _mockDb.Setup(db => db.Cuidadores).Returns(_mockCuidadores.Object);
        _mockDb.Setup(db => db.LecturasSensores).Returns(_mockLecturas.Object);
    }

    private void SetDuenoAuth(string userId = "user123") => _client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
            TestTokenHelper.GenerateDuenoToken(userId));

    private void SetupOwnership(string pacienteId, string userId)
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new Paciente { Id = pacienteId, UsuarioWebId = userId, Nombre = "Test" });
    }

    private static Mock<UpdateResult> MockUpdateResult(long modifiedCount = 1)
    {
        var m = new Mock<UpdateResult>();
        m.Setup(r => r.ModifiedCount).Returns(modifiedCount);
        return m;
    }

    [Fact]
    public async Task ObtenerPredicciones_ConDatos_Retorna200()
    {
        SetupOwnership("pac123", "user123");

        var predicciones = new List<PrediccionMl>
        {
            new() { Id = "p1", PacienteId = "pac123", ProbabilidadPico = 0.85, NivelRiesgo = "alto", Recomendacion = "Monitorear", FechaPrediccion = DateTime.UtcNow, HorasEstimadas = 2, ModeloVersion = "v1" }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<PrediccionMl>>(),
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(predicciones);

        SetDuenoAuth();
        var response = await _client.GetAsync("/api/ML/predicciones/pac123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ObtenerPredicciones_SinDatos_Retorna200()
    {
        SetupOwnership("pac123", "user123");

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<PrediccionMl>>(),
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<PrediccionMl>());

        SetDuenoAuth();
        var response = await _client.GetAsync("/api/ML/predicciones/pac123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task PrediccionActual_ConDatos_Retorna200()
    {
        SetupOwnership("pac123", "user123");

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<PrediccionMl>>(),
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>?>()))
            .ReturnsAsync(new PrediccionMl
            {
                Id = "p1", ProbabilidadPico = 0.75, NivelRiesgo = "medio",
                Recomendacion = "Revisar", FechaPrediccion = DateTime.UtcNow, HorasEstimadas = 1
            });

        SetDuenoAuth();
        var response = await _client.GetAsync("/api/ML/predicciones/pac123/actual");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("probabilidad").GetDouble().Should().Be(0.75);
    }

    [Fact]
    public async Task PrediccionActual_SinDatos_Retorna200()
    {
        SetupOwnership("pac123", "user123");

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<PrediccionMl>>(),
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>?>()))
            .ReturnsAsync((PrediccionMl?)null);

        SetDuenoAuth();
        var response = await _client.GetAsync("/api/ML/predicciones/pac123/actual");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Sin predicción activa");
    }

    [Fact]
    public async Task Recomendaciones_ConDatos_Retorna200()
    {
        SetupOwnership("pac123", "user123");

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<PrediccionMl>>(),
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<PrediccionMl>());

        SetDuenoAuth();
        var response = await _client.GetAsync("/api/ML/recomendaciones/pac123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListarModelos_ConDatos_Retorna200()
    {
        var modelos = new List<ModeloMl>
        {
            new() { Id = "m1", Version = "1.0", Accuracy = 0.95, Precision = 0.94, Recall = 0.93, F1Score = 0.935, Activo = true, TotalMuestras = 1000, FechaEntrenamiento = DateTime.UtcNow, Descripcion = "Modelo inicial" }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<ModeloMl>>(),
                It.IsAny<FilterDefinition<ModeloMl>>(),
                It.IsAny<SortDefinition<ModeloMl>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(modelos);

        SetDuenoAuth();
        var response = await _client.GetAsync("/api/ML/modelos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task EntrenarModelo_DatosValidos_Retorna200()
    {
        _mockModelos.Setup(c => c.InsertOneAsync(
                It.IsAny<ModeloMl>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SetDuenoAuth();
        var request = new EntrenarModeloRequest("2.0", "Segundo entrenamiento");
        var response = await _client.PostAsJsonAsync("/api/ML/entrenar", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Entrenamiento iniciado");
    }

    [Fact]
    public async Task ReentrenarModelo_ConModeloActivo_Retorna200()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<ModeloMl>>(),
                It.IsAny<FilterDefinition<ModeloMl>>(),
                It.IsAny<SortDefinition<ModeloMl>?>()))
            .ReturnsAsync(new ModeloMl { Id = "m1", Version = "1.0", Accuracy = 0.95, Precision = 0.94, Recall = 0.93, F1Score = 0.935 });

        _mockModelos.Setup(c => c.InsertOneAsync(
                It.IsAny<ModeloMl>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SetDuenoAuth();
        var request = new EntrenarModeloRequest("2.0", "Reentrenamiento");
        var response = await _client.PostAsJsonAsync("/api/ML/reentrenar", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Re-entrenamiento iniciado");
    }

    [Fact]
    public async Task ReentrenarModelo_SinModeloActivo_Retorna200()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<ModeloMl>>(),
                It.IsAny<FilterDefinition<ModeloMl>>(),
                It.IsAny<SortDefinition<ModeloMl>?>()))
            .ReturnsAsync((ModeloMl?)null);

        _mockModelos.Setup(c => c.InsertOneAsync(
                It.IsAny<ModeloMl>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SetDuenoAuth();
        var request = new EntrenarModeloRequest("1.0", "Primer modelo");
        var response = await _client.PostAsJsonAsync("/api/ML/reentrenar", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Diagnosticar_ConPrediccion_Retorna200()
    {
        SetupOwnership("pac123", "user123");

        var lecturas = new List<LecturaSensor>
        {
            new() { Id = "l1", Meta = new MetaData { PacienteId = "pac123" }, Timestamp = DateTime.UtcNow, PulsoBpm = 85, TemperaturaC = 36.8, SudoracionGsr = 3.2, ProbabilidadPico = 0.2 }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<LecturaSensor>>(),
                It.IsAny<FilterDefinition<LecturaSensor>>(),
                It.IsAny<SortDefinition<LecturaSensor>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(lecturas);

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<PrediccionMl>>(),
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>?>()))
            .ReturnsAsync(new PrediccionMl
            {
                Id = "p1", PacienteId = "pac123", ProbabilidadPico = 0.9, NivelRiesgo = "critico",
                Recomendacion = "Urgente", HorasEstimadas = 1, FechaPrediccion = DateTime.UtcNow, ModeloVersion = "v1"
            });

        SetDuenoAuth();
        var request = new DiagnosticarRequest("pac123");
        var response = await _client.PostAsJsonAsync("/api/ML/diagnosticar", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("nivelRiesgo").GetString().Should().Be("critico");
    }

    [Fact]
    public async Task Diagnosticar_SinLecturas_Retorna200()
    {
        SetupOwnership("pac123", "user123");

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<LecturaSensor>>(),
                It.IsAny<FilterDefinition<LecturaSensor>>(),
                It.IsAny<SortDefinition<LecturaSensor>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<LecturaSensor>());

        SetDuenoAuth();
        var request = new DiagnosticarRequest("pac123");
        var response = await _client.PostAsJsonAsync("/api/ML/diagnosticar", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Sin lecturas de sensores para diagnóstico");
    }

    [Fact]
    public async Task Diagnosticar_SinPrediccionPrevia_Retorna503()
    {
        SetupOwnership("pac123", "user123");

        var lecturas = new List<LecturaSensor>
        {
            new() { Id = "l1", Meta = new MetaData { PacienteId = "pac123" }, Timestamp = DateTime.UtcNow, PulsoBpm = 85, TemperaturaC = 36.8, SudoracionGsr = 3.2, ProbabilidadPico = 0.2 }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<LecturaSensor>>(),
                It.IsAny<FilterDefinition<LecturaSensor>>(),
                It.IsAny<SortDefinition<LecturaSensor>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(lecturas);

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<PrediccionMl>>(),
                It.IsAny<FilterDefinition<PrediccionMl>>(),
                It.IsAny<SortDefinition<PrediccionMl>?>()))
            .ReturnsAsync((PrediccionMl?)null);

        SetDuenoAuth();
        var request = new DiagnosticarRequest("pac123");
        var response = await _client.PostAsJsonAsync("/api/ML/diagnosticar", request);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task MetricasModelo_Existe_Retorna200()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<ModeloMl>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<ModeloMl, bool>>>()))
            .ReturnsAsync(new ModeloMl { Id = "m1", Version = "1.0", Accuracy = 0.95, Precision = 0.94, Recall = 0.93, F1Score = 0.935, TotalMuestras = 1000, FechaEntrenamiento = DateTime.UtcNow });

        SetDuenoAuth();
        var response = await _client.GetAsync("/api/ML/metricas/m1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("accuracy").GetDouble().Should().Be(0.95);
    }

    [Fact]
    public async Task MetricasModelo_NoExiste_Retorna404()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<ModeloMl>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<ModeloMl, bool>>>()))
            .ReturnsAsync((ModeloMl?)null);

        SetDuenoAuth();
        var response = await _client.GetAsync("/api/ML/metricas/nonexistent");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ML_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/ML/modelos");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ML_SinOwnership_Retorna403()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync((Paciente?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken("other_user"));

        var response = await _client.GetAsync("/api/ML/predicciones/pac123");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
