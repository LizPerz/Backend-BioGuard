using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace Test1BioGuard.IntegrationTests;

public class ReportesIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<LecturaSensor>> _mockLecturas;
    private readonly Mock<IMongoCollection<EventoMetabolico>> _mockEventos;
    private readonly Mock<IMongoCollection<Alerta>> _mockAlertas;
    private readonly Mock<IMongoCollection<Medicamento>> _mockMedicamentos;
    private readonly Mock<IMongoCollection<Paciente>> _mockPacientes;

    public ReportesIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;
        _mockLecturas = new Mock<IMongoCollection<LecturaSensor>>();
        _mockEventos = new Mock<IMongoCollection<EventoMetabolico>>();
        _mockAlertas = new Mock<IMongoCollection<Alerta>>();
        _mockMedicamentos = new Mock<IMongoCollection<Medicamento>>();
        _mockPacientes = new Mock<IMongoCollection<Paciente>>();
        _mockDb.Setup(db => db.LecturasSensores).Returns(_mockLecturas.Object);
        _mockDb.Setup(db => db.EventosMetabolicos).Returns(_mockEventos.Object);
        _mockDb.Setup(db => db.Alertas).Returns(_mockAlertas.Object);
        _mockDb.Setup(db => db.Medicamentos).Returns(_mockMedicamentos.Object);
        _mockDb.Setup(db => db.Pacientes).Returns(_mockPacientes.Object);
    }

    [Fact]
    public async Task Resumen_PacienteConDatos_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<LecturaSensor>>(),
                It.IsAny<FilterDefinition<LecturaSensor>>(),
                It.IsAny<SortDefinition<LecturaSensor>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<LecturaSensor>
            {
                new() { Id = "l1", PulsoBpm = 80, TemperaturaC = 36.5, Timestamp = DateTime.UtcNow }
            });

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<EventoMetabolico>>(),
                It.IsAny<FilterDefinition<EventoMetabolico>>(),
                It.IsAny<SortDefinition<EventoMetabolico>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<EventoMetabolico>());

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Alerta>>(),
                It.IsAny<FilterDefinition<Alerta>>(),
                It.IsAny<SortDefinition<Alerta>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<Alerta>());

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Medicamento>>(),
                It.IsAny<FilterDefinition<Medicamento>>(),
                It.IsAny<SortDefinition<Medicamento>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<Medicamento>());

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var response = await _client.GetAsync($"/api/Reportes/resumen/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("totalLecturas").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("totalEventos").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Resumen_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/Reportes/resumen/pac123");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HistorialAlertas_ConAlertas_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Alerta>>(),
                It.IsAny<FilterDefinition<Alerta>>(),
                It.IsAny<SortDefinition<Alerta>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<Alerta>
            {
                new() { Id = "a1", Tipo = "glucosa", Nivel = "critico", Titulo = "Alerta", Mensaje = "X", Atendida = false, FechaCreacion = DateTime.UtcNow }
            });

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var response = await _client.GetAsync($"/api/Reportes/historial-alertas/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task HistorialEventos_ConEventos_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<EventoMetabolico>>(),
                It.IsAny<FilterDefinition<EventoMetabolico>>(),
                It.IsAny<SortDefinition<EventoMetabolico>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<EventoMetabolico>
            {
                new() { Id = "e1", NivelRiesgo = "Critico", ProbabilidadMl = 0.92, Descripcion = "Pico", FechaEvento = DateTime.UtcNow }
            });

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var response = await _client.GetAsync($"/api/Reportes/historial-eventos/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task HistorialMedicamentos_ConMedicamentos_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Medicamento>>(),
                It.IsAny<FilterDefinition<Medicamento>>(),
                It.IsAny<SortDefinition<Medicamento>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<Medicamento>
            {
                new() { Id = "m1", Nombre = "Metformina", Dosis = "500mg", Horario = "8:00", Activo = true }
            });

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var response = await _client.GetAsync($"/api/Reportes/historial-medicamentos/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task HistorialLecturas_ConLecturas_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<LecturaSensor>>(),
                It.IsAny<FilterDefinition<LecturaSensor>>(),
                It.IsAny<SortDefinition<LecturaSensor>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<LecturaSensor>
            {
                new() { Id = "l1", PulsoBpm = 80, TemperaturaC = 36.5, Timestamp = DateTime.UtcNow }
            });

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var response = await _client.GetAsync($"/api/Reportes/historial-lecturas/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }
}
