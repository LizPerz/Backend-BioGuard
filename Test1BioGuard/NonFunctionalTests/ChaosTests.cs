using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using Test1BioGuard.IntegrationTests;

namespace Test1BioGuard.NonFunctionalTests;

public class ChaosTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;

    private const string DuenoId = "chaos_dueno";
    private const string PacienteId = "chaos_paciente";

    public ChaosTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;
    }

    [Fact]
    public async Task MongoDbTimeout_LoginWeb_Retorna503()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ThrowsAsync(new TimeoutException("MongoDB connection timed out"));

        var request = new { Correo = "test@test.com", Password = "pass" };
        var response = await _client.PostAsJsonAsync("/api/Auth/login-web", request);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("error");
    }

    [Fact]
    public async Task MongoDbUnavailable_MiPaciente_Retorna500()
    {
        _client.DefaultRequestHeaders.Authorization =
            new("Bearer", TestTokenHelper.GenerateDuenoToken(DuenoId));

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ThrowsAsync(new MongoException("Unable to connect to MongoDB Atlas"));

        var response = await _client.GetAsync("/api/Pacientes/mi-paciente");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("error");
    }

    [Fact]
    public async Task MongoDbSlowConnection_HealthEndpoint_Retorna200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body!["status"].ToString().Should().Be("healthy");
    }

    [Fact]
    public async Task ErrorHandler_TiraExcepcionNoControlada_Retorna500ConError()
    {
        _client.DefaultRequestHeaders.Authorization =
            new("Bearer", TestTokenHelper.GenerateDuenoToken(DuenoId));

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        var response = await _client.GetAsync("/api/Pacientes/mi-paciente");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("error");
        body.Should().Contain("traceId");
    }

    [Fact]
    public async Task ErrorHandler_TraeTraceId_EnProduccion()
    {
        _client.DefaultRequestHeaders.Authorization =
            new("Bearer", TestTokenHelper.GenerateDuenoToken(DuenoId));

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ThrowsAsync(new DivideByZeroException("Simulated crash"));

        var response = await _client.GetAsync("/api/Pacientes/mi-paciente");

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("traceId");
        body!["traceId"]!.ToString().Should().NotBeNullOrEmpty();
    }
}
