using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace Test1BioGuard.IntegrationTests;

public class PlanesIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<Plan>> _mockPlanes;

    public PlanesIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;

        _mockPlanes = new Mock<IMongoCollection<Plan>>();
        _mockDb.Setup(db => db.Planes).Returns(_mockPlanes.Object);
    }

    // ── GET /api/Planes ─────────────────────────────────────

    [Fact]
    public async Task Listar_PlanesActivos_Retorna200()
    {
        var planes = new List<Plan>
        {
            new Plan { Id = "p1", Nombre = "Básico", Precio = 9.99m, PrecioMoneda = "USD", LimitePacientes = 1, LimiteCuidadores = 2, DiasHistorial = 30, GpsContinuo = false, AiConsole = false, Activo = true, Orden = 1, Descripcion = "Plan básico" },
            new Plan { Id = "p2", Nombre = "Premium", Precio = 19.99m, PrecioMoneda = "USD", LimitePacientes = 1, LimiteCuidadores = 5, DiasHistorial = 90, GpsContinuo = true, AiConsole = true, Activo = true, Orden = 2, Descripcion = "Plan premium" }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<FilterDefinition<Plan>>(),
                It.IsAny<SortDefinition<Plan>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(planes);

        var response = await _client.GetAsync("/api/Planes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;
        arr.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Listar_SinPlanes_RetornaListaVacia()
    {
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<FilterDefinition<Plan>>(),
                It.IsAny<SortDefinition<Plan>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<Plan>());

        var response = await _client.GetAsync("/api/Planes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Listar_NoRequiereAuth()
    {
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<FilterDefinition<Plan>>(),
                It.IsAny<SortDefinition<Plan>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<Plan>());

        var response = await _client.GetAsync("/api/Planes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GET /api/Planes/{id} ────────────────────────────────

    [Fact]
    public async Task GetById_PlanExiste_Retorna200()
    {
        var plan = new Plan
        {
            Id = "plan123",
            Nombre = "Premium",
            Precio = 19.99m,
            PrecioMoneda = "USD",
            LimitePacientes = 1,
            LimiteCuidadores = 5,
            DiasHistorial = 90,
            GpsContinuo = true,
            AiConsole = true,
            Activo = true,
            Orden = 2,
            Descripcion = "Plan premium completo"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        var response = await _client.GetAsync("/api/Planes/plan123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("nombre").GetString().Should().Be("Premium");
        doc.RootElement.GetProperty("precio").GetDecimal().Should().Be(19.99m);
        doc.RootElement.GetProperty("gpsContinuo").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetById_PlanNoExiste_Retorna404()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync((Plan?)null);

        var response = await _client.GetAsync("/api/Planes/invalidId");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_NoRequiereAuth()
    {
        var plan = new Plan
        {
            Id = "plan123",
            Nombre = "Premium",
            Precio = 19.99m,
            PrecioMoneda = "USD",
            LimitePacientes = 1,
            LimiteCuidadores = 5,
            DiasHistorial = 90,
            GpsContinuo = true,
            AiConsole = true,
            Activo = true,
            Orden = 2,
            Descripcion = "Plan premium completo"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        var response = await _client.GetAsync("/api/Planes/anyId");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_TodosLosCampos_RetornaCorrectamente()
    {
        var plan = new Plan
        {
            Id = "plan456",
            Nombre = "Básico",
            Precio = 9.99m,
            PrecioMoneda = "EUR",
            LimitePacientes = 1,
            LimiteCuidadores = 2,
            DiasHistorial = 30,
            GpsContinuo = false,
            AiConsole = false,
            Activo = true,
            Orden = 1,
            Descripcion = "Plan básico"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        var response = await _client.GetAsync("/api/Planes/plan456");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("id").GetString().Should().Be("plan456");
        doc.RootElement.GetProperty("precioMoneda").GetString().Should().Be("EUR");
        doc.RootElement.GetProperty("limitePacientes").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("limiteCuidadores").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("diasHistorial").GetInt32().Should().Be(30);
        doc.RootElement.GetProperty("gpsContinuo").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("aiConsole").GetBoolean().Should().BeFalse();
    }
}
