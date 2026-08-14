using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using BioGuard.Api.Services;
using Test1BioGuard.IntegrationTests;

namespace Test1BioGuard.NonFunctionalTests;

public class SmokeTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;

    public SmokeTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;
    }

    [Fact]
    public async Task HealthEndpoint_Retorna200ConStatusHealthy()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("status");
        body!["status"].ToString().Should().Be("healthy");
        body.Should().ContainKey("timestamp");
    }

    [Fact]
    public async Task LoginWeb_CredencialesValidas_Retorna200ConToken()
    {
        var user = new UsuarioWeb
        {
            Id = "smoke_user",
            Correo = "smoke@bioguard.test",
            PasswordHash = PasswordHasher.Hash("Test@123!"),
            Activo = true
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);

        var request = new { Correo = "smoke@bioguard.test", Password = "Test@123!" };
        var response = await _client.PostAsJsonAsync("/api/Auth/login-web", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("token");
    }

    [Fact]
    public async Task LecturaInsert_DatosValidos_Retorna200ConLecturaId()
    {
        var pacienteId = "smoke_paciente_123";
        _client.DefaultRequestHeaders.Authorization =
            new("Bearer", TestTokenHelper.GeneratePacienteToken(pacienteId));

        var payload = new
        {
            pulsoBpm = 72,
            temperaturaC = 36.5,
            estresPct = 35.0
        };

        var response = await _client.PostAsJsonAsync("/api/Sensores/lectura", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("lecturaId");
    }
}
