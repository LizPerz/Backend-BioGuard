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

public class DispositivosIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<Dispositivo>> _mockDispositivos;

    public DispositivosIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;
        _mockDispositivos = new Mock<IMongoCollection<Dispositivo>>();
        _mockDb.Setup(db => db.Dispositivos).Returns(_mockDispositivos.Object);
    }

    [Fact]
    public async Task Vincular_DispositivoNuevo_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Dispositivo>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Dispositivo, bool>>>()))
            .ReturnsAsync((Dispositivo?)null);

        _mockDispositivos.Setup(c => c.InsertOneAsync(
            It.IsAny<Dispositivo>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var request = new VincularDispositivoRequest("Galaxy Watch 6", "AA:BB:CC:DD:EE:FF");
        var response = await _client.PostAsJsonAsync("/api/Dispositivos/vincular", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Dispositivo vinculado");
    }

    [Fact]
    public async Task Vincular_YaTieneDispositivo_Retorna400()
    {
        var pacienteId = "123456789012345678901234";
        var dispositivoExistente = new Dispositivo
        {
            Id = "disp1", PacienteId = pacienteId, NombreDispositivo = "Watch"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Dispositivo>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Dispositivo, bool>>>()))
            .ReturnsAsync(dispositivoExistente);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var request = new VincularDispositivoRequest("Otro Watch", "FF:FF:FF:FF:FF:FF");
        var response = await _client.PostAsJsonAsync("/api/Dispositivos/vincular", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ObtenerPorPaciente_SinDispositivo_RetornaVinculadoFalse()
    {
        var pacienteId = "123456789012345678901234";

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Dispositivo>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Dispositivo, bool>>>()))
            .ReturnsAsync((Dispositivo?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Dispositivos/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("vinculado").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Vincular_SinToken_Retorna401()
    {
        var response = await _client.PostAsJsonAsync("/api/Dispositivos/vincular",
            new VincularDispositivoRequest("Watch", "AA:BB:CC"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Heartbeat_DispositivoExiste_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockDispositivos.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<Dispositivo>>(),
                It.IsAny<UpdateDefinition<Dispositivo>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var request = new HeartbeatRequest(pacienteId);
        var response = await _client.PostAsJsonAsync("/api/Dispositivos/heartbeat", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Heartbeat recibido");
    }

    // ── PUT /api/Dispositivos/{id} ─────────────────────────

    [Fact]
    public async Task Actualizar_DispositivoExiste_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockDispositivos.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<Dispositivo>>(),
                It.IsAny<UpdateDefinition<Dispositivo>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var request = new { Nombre = "Watch 7 Pro" };
        var response = await _client.PutAsJsonAsync("/api/Dispositivos/disp1", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Dispositivo actualizado");
    }

    // ── DELETE /api/Dispositivos/{id} ───────────────────────

    [Fact]
    public async Task Desvincular_DispositivoExiste_Retorna204()
    {
        var pacienteId = "123456789012345678901234";
        var mockResult = new Mock<DeleteResult>();
        mockResult.Setup(r => r.DeletedCount).Returns(1);

        _mockDispositivos.Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Dispositivo>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var response = await _client.DeleteAsync("/api/Dispositivos/disp1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Desvincular_SinToken_Retorna401()
    {
        var response = await _client.DeleteAsync("/api/Dispositivos/disp1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
