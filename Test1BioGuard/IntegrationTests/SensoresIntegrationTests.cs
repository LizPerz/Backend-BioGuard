using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;
using BioGuard.Api.Controllers;

namespace Test1BioGuard.IntegrationTests;

public class SensoresIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<LecturaSensor>> _mockLecturas;
    private readonly Mock<IMongoCollection<EventoMetabolico>> _mockEventos;
    private readonly Mock<IMongoCollection<TrackingGps>> _mockTracking;

    public SensoresIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;

        _mockLecturas = new Mock<IMongoCollection<LecturaSensor>>();
        _mockEventos = new Mock<IMongoCollection<EventoMetabolico>>();
        _mockTracking = new Mock<IMongoCollection<TrackingGps>>();

        _mockDb.Setup(db => db.LecturasSensores).Returns(_mockLecturas.Object);
        _mockDb.Setup(db => db.EventosMetabolicos).Returns(_mockEventos.Object);
        _mockDb.Setup(db => db.TrackingGps).Returns(_mockTracking.Object);
    }

    [Fact]
    public async Task ObtenerLecturas_PacienteConLecturas_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var lecturas = new List<LecturaSensor>
        {
            new()
            {
                Id = "lec1",
                Meta = new MetaData { PacienteId = pacienteId },
                PulsoBpm = 75,
                TemperaturaC = 36.5,
                SudoracionGsr = 12.0,
                ProbabilidadPico = 0.2,
                Timestamp = DateTime.UtcNow
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<LecturaSensor>>(),
                It.IsAny<FilterDefinition<LecturaSensor>>(),
                It.IsAny<SortDefinition<LecturaSensor>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(lecturas);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Sensores/lecturas/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ObtenerLecturas_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/Sensores/lecturas/123456789012345678901234");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Estadisticas_PacienteConDatos_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var lecturas = new List<LecturaSensor>
        {
            new()
            {
                Id = "lec1",
                PulsoBpm = 80,
                TemperaturaC = 36.5,
                SudoracionGsr = 10.0,
                ProbabilidadPico = 0.3,
                Timestamp = DateTime.UtcNow,
                Meta = new MetaData { PacienteId = pacienteId }
            },
            new()
            {
                Id = "lec2",
                PulsoBpm = 90,
                TemperaturaC = 37.0,
                SudoracionGsr = 15.0,
                ProbabilidadPico = 0.7,
                Timestamp = DateTime.UtcNow.AddSeconds(-10),
                Meta = new MetaData { PacienteId = pacienteId }
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<LecturaSensor>>(),
                It.IsAny<FilterDefinition<LecturaSensor>>(),
                It.IsAny<SortDefinition<LecturaSensor>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(lecturas);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Sensores/estadisticas/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("totalLecturas").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Estadisticas_SinDatos_RetornaSinDatos()
    {
        var pacienteId = "123456789012345678901234";
        var lecturas = new List<LecturaSensor>();

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<LecturaSensor>>(),
                It.IsAny<FilterDefinition<LecturaSensor>>(),
                It.IsAny<SortDefinition<LecturaSensor>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(lecturas);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Sensores/estadisticas/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Sin datos");
    }

    [Fact]
    public async Task ObtenerEventos_PacienteConEventos_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var eventos = new List<EventoMetabolico>
        {
            new()
            {
                Id = "evt1",
                PacienteId = pacienteId,
                NivelRiesgo = "Pre-Pico",
                ProbabilidadMl = 0.87,
                Descripcion = "Elevacion detectada",
                FechaEvento = DateTime.UtcNow,
                Atendida = false
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<EventoMetabolico>>(),
                It.IsAny<FilterDefinition<EventoMetabolico>>(),
                It.IsAny<SortDefinition<EventoMetabolico>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(eventos);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Sensores/eventos/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ResumenEventos_RetornaResumen()
    {
        var pacienteId = "123456789012345678901234";
        var eventos = new List<EventoMetabolico>
        {
            new() { Id = "e1", PacienteId = pacienteId, NivelRiesgo = "Critico", ProbabilidadMl = 0.95, FechaEvento = DateTime.UtcNow, Atendida = false },
            new() { Id = "e2", PacienteId = pacienteId, NivelRiesgo = "Pre-Pico", ProbabilidadMl = 0.88, FechaEvento = DateTime.UtcNow, Atendida = true },
            new() { Id = "e3", PacienteId = pacienteId, NivelRiesgo = "Normal", ProbabilidadMl = 0.3, FechaEvento = DateTime.UtcNow, Atendida = false }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<EventoMetabolico>>(),
                It.IsAny<FilterDefinition<EventoMetabolico>>(),
                It.IsAny<SortDefinition<EventoMetabolico>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(eventos);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Sensores/eventos/{pacienteId}/resumen");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("total").GetInt32().Should().Be(3);
        doc.RootElement.GetProperty("criticos").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("atendidos").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task TrackingActual_SinUbicacion_Retorna404()
    {
        var pacienteId = "123456789012345678901234";

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<TrackingGps>>(),
                It.IsAny<FilterDefinition<TrackingGps>>(),
                It.IsAny<SortDefinition<TrackingGps>>()))
            .ReturnsAsync((TrackingGps?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Sensores/tracking/{pacienteId}/actual");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RecibirLectura_LecturaValida_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        _mockLecturas.Setup(c => c.InsertOneAsync(
            It.IsAny<LecturaSensor>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var request = new LecturaSensorRequest(75, 36.5, 12.0);
        var response = await _client.PostAsJsonAsync("/api/Sensores/lectura", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Lectura recibida");
    }

    [Fact]
    public async Task ObtenerLecturasRango_ConRango_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var lecturas = new List<LecturaSensor>
        {
            new()
            {
                Id = "lec1",
                Meta = new MetaData { PacienteId = pacienteId },
                PulsoBpm = 75,
                TemperaturaC = 36.5,
                SudoracionGsr = 12.0,
                ProbabilidadPico = 0.2,
                Timestamp = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc)
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<LecturaSensor>>(),
                It.IsAny<FilterDefinition<LecturaSensor>>(),
                It.IsAny<SortDefinition<LecturaSensor>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(lecturas);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Sensores/lecturas/{pacienteId}/rango?desde=2024-01-01T00:00:00Z&hasta=2024-12-31T23:59:59Z");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Tendencia_ConDatos_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var lecturas = new List<LecturaSensor>
        {
            new()
            {
                Id = "lec1",
                Meta = new MetaData { PacienteId = pacienteId },
                PulsoBpm = 80,
                TemperaturaC = 36.5,
                ProbabilidadPico = 0.3,
                Timestamp = DateTime.UtcNow.AddHours(-12)
            },
            new()
            {
                Id = "lec2",
                Meta = new MetaData { PacienteId = pacienteId },
                PulsoBpm = 90,
                TemperaturaC = 37.0,
                ProbabilidadPico = 0.7,
                Timestamp = DateTime.UtcNow
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<LecturaSensor>>(),
                It.IsAny<FilterDefinition<LecturaSensor>>(),
                It.IsAny<SortDefinition<LecturaSensor>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(lecturas);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Sensores/estadisticas/{pacienteId}/tendencia?periodo=diario");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task CrearEvento_DatosValidos_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        _mockEventos.Setup(c => c.InsertOneAsync(
            It.IsAny<EventoMetabolico>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var request = new CrearEventoRequest(0.92, "Critico", "Pico detectado");
        var response = await _client.PostAsJsonAsync("/api/Sensores/evento", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Evento creado");
    }

    [Fact]
    public async Task AtenderEvento_DatosValidos_Retorna200()
    {
        var eventoId = "evt1234567890123456789";

        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockEventos.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<EventoMetabolico>>(),
            It.IsAny<UpdateDefinition<EventoMetabolico>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new AtenderEventoRequest("cuidador1");
        var response = await _client.PutAsJsonAsync($"/api/Sensores/eventos/{eventoId}/atender", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Evento atendido");
    }

    [Fact]
    public async Task InsertarTracking_DatosValidos_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        _mockTracking.Setup(c => c.InsertOneAsync(
            It.IsAny<TrackingGps>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var request = new TrackingGpsRequest(-99.1, 19.4, false);
        var response = await _client.PostAsJsonAsync("/api/Sensores/tracking", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Tracking insertado");
    }

    [Fact]
    public async Task InsertarTrackingBatch_ListaValida_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        _mockTracking.Setup(c => c.InsertOneAsync(
            It.IsAny<TrackingGps>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var request = new List<TrackingGpsRequest>
        {
            new(-99.1, 19.4, false),
            new(-99.2, 19.5, true)
        };
        var response = await _client.PostAsJsonAsync("/api/Sensores/tracking-batch", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("procesadas").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task TrackingRuta_ConDatos_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var puntos = new List<TrackingGps>
        {
            new()
            {
                Id = "trk1",
                Meta = new MetaData { PacienteId = pacienteId },
                Timestamp = DateTime.UtcNow,
                Ubicacion = new UbicacionGps { Coordinates = new[] { -99.1, 19.4 } },
                EsEmergencia = false
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<TrackingGps>>(),
                It.IsAny<FilterDefinition<TrackingGps>>(),
                It.IsAny<SortDefinition<TrackingGps>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(puntos);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Sensores/tracking/{pacienteId}/ruta?desde=2024-01-01T00:00:00Z&hasta=2024-12-31T23:59:59Z");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ExportarPDF_ConLecturas_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var lecturas = new List<LecturaSensor>
        {
            new()
            {
                Id = "lec1",
                Meta = new MetaData { PacienteId = pacienteId },
                PulsoBpm = 75,
                TemperaturaC = 36.5,
                SudoracionGsr = 12.0,
                ProbabilidadPico = 0.2,
                Timestamp = DateTime.UtcNow
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<LecturaSensor>>(),
                It.IsAny<FilterDefinition<LecturaSensor>>(),
                It.IsAny<SortDefinition<LecturaSensor>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(lecturas);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Sensores/lecturas/{pacienteId}/exportar-pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Contain("PDF generado");
    }
}
