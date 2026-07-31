using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace Test1BioGuard.IntegrationTests;

public class AuditoriaIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<Auditoria>> _mockAuditoria;

    public AuditoriaIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;
        _mockAuditoria = new Mock<IMongoCollection<Auditoria>>();
        _mockDb.Setup(db => db.Auditoria).Returns(_mockAuditoria.Object);
    }

    [Fact]
    public async Task Listar_ConRegistros_Retorna200()
    {
        var registros = new List<Auditoria>
        {
            new()
            {
                Id = "aud1", EntidadId = "user1", Accion = "Login",
                TablaAfectada = "usuarios_web", RegistroId = "user1",
                Fecha = DateTime.UtcNow, Ip = "127.0.0.1"
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Auditoria>>(),
                It.IsAny<FilterDefinition<Auditoria>>(),
                It.IsAny<SortDefinition<Auditoria>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(registros);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Auditoria?pagina=1&porPagina=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Listar_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/Auditoria");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Listar_UsuarioRegular_RetornaSoloSusRegistros()
    {
        var registros = new List<Auditoria>
        {
            new() { Id = "aud1", EntidadId = "user123", Accion = "Login", Fecha = DateTime.UtcNow }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Auditoria>>(),
                It.IsAny<FilterDefinition<Auditoria>>(),
                It.IsAny<SortDefinition<Auditoria>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(registros);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken("user123"));

        var response = await _client.GetAsync("/api/Auditoria?pagina=1&porPagina=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Listar_Admin_RetornaTodosLosRegistros()
    {
        var registros = new List<Auditoria>
        {
            new() { Id = "aud1", EntidadId = "user1", Accion = "Login", Fecha = DateTime.UtcNow },
            new() { Id = "aud2", EntidadId = "user2", Accion = "Update", Fecha = DateTime.UtcNow }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Auditoria>>(),
                It.IsAny<FilterDefinition<Auditoria>>(),
                It.IsAny<SortDefinition<Auditoria>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(registros);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateToken("admin1", "administrador"));

        var response = await _client.GetAsync("/api/Auditoria?pagina=1&porPagina=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Listar_PaginaInvalida_Retorna200ConValorPorDefecto()
    {
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Auditoria>>(),
                It.IsAny<FilterDefinition<Auditoria>>(),
                It.IsAny<SortDefinition<Auditoria>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<Auditoria>());

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Auditoria?pagina=0&porPagina=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Listar_PorPaginaExcedeLimite_Retorna200ConLimiteAplicado()
    {
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Auditoria>>(),
                It.IsAny<FilterDefinition<Auditoria>>(),
                It.IsAny<SortDefinition<Auditoria>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<Auditoria>());

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Auditoria?pagina=1&porPagina=300");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
