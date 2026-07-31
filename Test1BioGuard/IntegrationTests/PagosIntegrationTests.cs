using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;
using BioGuard.Api.Services;

namespace Test1BioGuard.IntegrationTests;

public class PagosIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<Pago>> _mockPagos;
    private readonly Mock<IMongoCollection<Plan>> _mockPlanes;

    public PagosIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;
        _mockPagos = new Mock<IMongoCollection<Pago>>();
        _mockPlanes = new Mock<IMongoCollection<Plan>>();
        _mockDb.Setup(db => db.Pagos).Returns(_mockPagos.Object);
        _mockDb.Setup(db => db.Planes).Returns(_mockPlanes.Object);
    }

    [Fact]
    public async Task CrearSesion_PlanValido_Retorna200()
    {
        var plan = new Plan { Id = "plan1", Nombre = "Care", Precio = 129m, PrecioMoneda = "MXN" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);
        _mockPagos.Setup(c => c.InsertOneAsync(
            It.IsAny<Pago>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<Pago, InsertOneOptions, CancellationToken>((p, _, _) => p.Id = "pago_mock_id");

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new CrearSesionPagoRequest("Care", "stripe");
        var response = await _client.PostAsJsonAsync("/api/Pagos/crear-sesion", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("monto").GetDecimal().Should().Be(129m);
        doc.RootElement.GetProperty("pagoId").GetString().Should().NotBeNullOrEmpty();
        doc.RootElement.GetProperty("sesionUrl").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CrearSesion_PlanNoExiste_Retorna400()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync((Plan?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new CrearSesionPagoRequest("Inexistente", "stripe");
        var response = await _client.PostAsJsonAsync("/api/Pagos/crear-sesion", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CrearSesion_ProcesadorInvalido_Retorna400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new CrearSesionPagoRequest("Care", "bitcoin");
        var response = await _client.PostAsJsonAsync("/api/Pagos/crear-sesion", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Historial_ConPagos_Retorna200()
    {
        var pagos = new List<Pago>
        {
            new() { Id = "pago1", Monto = 129m, Moneda = "MXN", Estado = "completado",
                FechaPago = DateTime.UtcNow, MetodoPago = "stripe", UsuarioWebId = "user123" }
        };
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Pago>>(),
                It.IsAny<FilterDefinition<Pago>>(),
                It.IsAny<SortDefinition<Pago>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(pagos);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Pagos/historial");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Recibo_PagoExiste_Retorna200()
    {
        var pago = new Pago
        {
            Id = "pago1", Monto = 129m, Moneda = "MXN", Estado = "completado",
            FechaPago = DateTime.UtcNow, MetodoPago = "stripe", UsuarioWebId = "user123"
        };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Pago>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Pago, bool>>>()))
            .ReturnsAsync(pago);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Pagos/pago1/recibo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("monto").GetDecimal().Should().Be(129m);
        doc.RootElement.GetProperty("pagoId").GetString().Should().Be("pago1");
        doc.RootElement.GetProperty("descargaUrl").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Recibo_PagoNoExiste_Retorna404()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Pago>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Pago, bool>>>()))
            .ReturnsAsync((Pago?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Pagos/nonexistent/recibo");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Recibo_PagoDeOtroUsuario_Retorna403()
    {
        var pago = new Pago
        {
            Id = "pago1", Monto = 129m, Moneda = "MXN", Estado = "completado",
            FechaPago = DateTime.UtcNow, UsuarioWebId = "otro_usuario"
        };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Pago>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Pago, bool>>>()))
            .ReturnsAsync(pago);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Pagos/pago1/recibo");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DescargarRecibo_PagoExiste_RetornaFile()
    {
        var pago = new Pago
        {
            Id = "pago1", Monto = 129m, Moneda = "MXN", Estado = "completado",
            FechaPago = DateTime.UtcNow, MetodoPago = "stripe", UsuarioWebId = "user123", PlanId = "plan1"
        };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Pago>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Pago, bool>>>()))
            .ReturnsAsync(pago);

        var plan = new Plan { Id = "plan1", Nombre = "Care" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Pagos/pago1/recibo/descarga");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("RECIBO DE PAGO BIOGUARD");
        body.Should().Contain("pago1");
        body.Should().Contain("Care");
    }

    [Fact]
    public async Task DescargarRecibo_PagoDeOtroUsuario_Retorna403()
    {
        var pago = new Pago
        {
            Id = "pago1", Monto = 129m, Moneda = "MXN", Estado = "completado",
            FechaPago = DateTime.UtcNow, UsuarioWebId = "otro_usuario"
        };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Pago>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Pago, bool>>>()))
            .ReturnsAsync(pago);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Pagos/pago1/recibo/descarga");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cancelar_SinToken_Retorna401()
    {
        var response = await _client.PostAsJsonAsync("/api/Pagos/cancelar", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Historial_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/Pagos/historial");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CrearSesion_SinToken_Retorna401()
    {
        var response = await _client.PostAsJsonAsync("/api/Pagos/crear-sesion", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recibo_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/Pagos/p1/recibo");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
