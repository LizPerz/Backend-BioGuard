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
}
