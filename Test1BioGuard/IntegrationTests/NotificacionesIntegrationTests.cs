using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Controllers;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;

namespace Test1BioGuard.IntegrationTests;

public class NotificacionesIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<Notificacion>> _mockNotificaciones;

    public NotificacionesIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;
        _mockNotificaciones = new Mock<IMongoCollection<Notificacion>>();
        _mockDb.Setup(db => db.Notificaciones).Returns(_mockNotificaciones.Object);
    }

    [Fact]
    public async Task ObtenerPorPaciente_ConNotificaciones_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var notificaciones = new List<Notificacion>
        {
            new()
            {
                Id = "not1", PacienteId = pacienteId, Titulo = "Alerta",
                Mensaje = "Pulso alto", Tipo = "alerta", Leida = false, FechaEnvio = DateTime.UtcNow
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Notificacion>>(),
                It.IsAny<FilterDefinition<Notificacion>>(),
                It.IsAny<SortDefinition<Notificacion>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(notificaciones);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Notificaciones/by-paciente/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ObtenerPorPaciente_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/Notificaciones/by-paciente/pac123");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarcarLeida_NotificacionExiste_Retorna200()
    {
        var mockUpdateResult = new Mock<UpdateResult>();
        mockUpdateResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockNotificaciones.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<Notificacion>>(),
                It.IsAny<UpdateDefinition<Notificacion>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpdateResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.PutAsJsonAsync("/api/Notificaciones/not1/leer", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Crear_DatosValidos_Retorna200()
    {
        _mockNotificaciones.Setup(c => c.InsertOneAsync(
            It.IsAny<Notificacion>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new CrearNotificacionRequest("pac123", "Alerta", "Pulso alto", "alerta");
        var response = await _client.PostAsJsonAsync("/api/Notificaciones", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Notificación creada");
    }
}
