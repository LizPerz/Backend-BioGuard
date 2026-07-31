using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace Test1BioGuard.IntegrationTests;

public class AdminIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<UsuarioWeb>> _mockUsuarios;
    private readonly Mock<IMongoCollection<Auditoria>> _mockAuditoria;
    private readonly Mock<IMongoCollection<Paciente>> _mockPacientes;
    private readonly Mock<IMongoCollection<Alerta>> _mockAlertas;
    private readonly Mock<IMongoCollection<Plan>> _mockPlanes;
    private readonly Mock<IMongoCollection<TicketSoporte>> _mockTickets;

    private const string AdminToken = "admin_token";
    private const string AdminUserId = "admin1";

    public AdminIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;

        _mockUsuarios = new Mock<IMongoCollection<UsuarioWeb>>();
        _mockAuditoria = new Mock<IMongoCollection<Auditoria>>();
        _mockPacientes = new Mock<IMongoCollection<Paciente>>();
        _mockAlertas = new Mock<IMongoCollection<Alerta>>();
        _mockPlanes = new Mock<IMongoCollection<Plan>>();
        _mockTickets = new Mock<IMongoCollection<TicketSoporte>>();

        _mockDb.Setup(db => db.UsuariosWeb).Returns(_mockUsuarios.Object);
        _mockDb.Setup(db => db.Auditoria).Returns(_mockAuditoria.Object);
        _mockDb.Setup(db => db.Pacientes).Returns(_mockPacientes.Object);
        _mockDb.Setup(db => db.Alertas).Returns(_mockAlertas.Object);
        _mockDb.Setup(db => db.Planes).Returns(_mockPlanes.Object);
        _mockDb.Setup(db => db.TicketsSoporte).Returns(_mockTickets.Object);
    }

    private string GenerateAdminToken()
        => TestTokenHelper.GenerateToken(AdminUserId, "administrador");

    [Fact]
    public async Task GetUsuarios_ConCorreo_Retorna200()
    {
        var usuarios = new List<UsuarioWeb>
        {
            new() { Id = "u1", Correo = "test@test.com", Nombre = "Test", Activo = true }
        };
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<FilterDefinition<UsuarioWeb>>(),
                It.IsAny<SortDefinition<UsuarioWeb>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(usuarios);

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(new List<Plan>());

        _client.DefaultRequestHeaders.Authorization =
            new("Bearer", GenerateAdminToken());

        var response = await _client.GetAsync("/api/Admin/usuarios?correo=test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("usuarios").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("usuarios")[0].GetProperty("correo").GetString().Should().Be("test@test.com");
    }

    [Fact]
    public async Task GetUsuarios_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/Admin/usuarios");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUsuarios_ConRolPaciente_Retorna403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new("Bearer", TestTokenHelper.GeneratePacienteToken("pac123"));
        var response = await _client.GetAsync("/api/Admin/usuarios");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PausarUsuario_Pausar_Retorna200()
    {
        var usuario = new UsuarioWeb { Id = "u1", Correo = "test@test.com", Activo = true };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(usuario);

        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new("Bearer", GenerateAdminToken());

        var request = new { Pausar = true, Motivo = "Mantenimiento" };
        var response = await _client.PutAsJsonAsync("/api/Admin/usuarios/u1/pausar", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Cuenta pausada");
    }

    [Fact]
    public async Task PausarUsuario_Reactivar_Retorna200()
    {
        var usuario = new UsuarioWeb { Id = "u1", Correo = "test@test.com", Activo = false };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(usuario);

        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new("Bearer", GenerateAdminToken());

        var request = new { Pausar = false };
        var response = await _client.PutAsJsonAsync("/api/Admin/usuarios/u1/pausar", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Cuenta reactivada");
    }

    [Fact]
    public async Task PausarUsuario_SinMotivoAlPausar_Retorna400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new("Bearer", GenerateAdminToken());

        var request = new { Pausar = true };
        var response = await _client.PutAsJsonAsync("/api/Admin/usuarios/u1/pausar", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PausarUsuario_UsuarioNoExiste_Retorna404()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);

        _client.DefaultRequestHeaders.Authorization =
            new("Bearer", GenerateAdminToken());

        var request = new { Pausar = true, Motivo = "Mantenimiento" };
        var response = await _client.PutAsJsonAsync("/api/Admin/usuarios/nonexistent/pausar", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ObtenerMetricas_Retorna200()
    {
        _mockDb.Setup(db => db.CountDocumentsAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(10L);

        _mockDb.Setup(db => db.CountDocumentsAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(25L);

        _mockDb.Setup(db => db.CountDocumentsAsync(
                It.IsAny<IMongoCollection<Alerta>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Alerta, bool>>>()))
            .ReturnsAsync(5L);

        var planes = new List<Plan>
        {
            new() { Id = "p1", Nombre = "Free", Precio = 0m }
        };
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(planes);

        _client.DefaultRequestHeaders.Authorization =
            new("Bearer", GenerateAdminToken());

        var response = await _client.GetAsync("/api/Admin/metricas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("usuariosActivos").GetInt32().Should().Be(10);
        doc.RootElement.GetProperty("pacientesActivos").GetInt32().Should().Be(25);
        doc.RootElement.GetProperty("alertasCriticasDesde").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task ListarTickets_Retorna200()
    {
        var tickets = new List<TicketSoporte>
        {
            new() { Id = "t1", UsuarioId = "u1", Asunto = "Problema", Estado = "abierto", FechaCreacion = DateTime.UtcNow }
        };
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<TicketSoporte>>(),
                It.IsAny<FilterDefinition<TicketSoporte>>(),
                It.IsAny<SortDefinition<TicketSoporte>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(tickets);

        _client.DefaultRequestHeaders.Authorization =
            new("Bearer", GenerateAdminToken());

        var response = await _client.GetAsync("/api/Admin/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ListarTickets_FiltradosPorEstado_Retorna200()
    {
        var tickets = new List<TicketSoporte>
        {
            new() { Id = "t1", UsuarioId = "u1", Asunto = "Problema", Estado = "abierto", FechaCreacion = DateTime.UtcNow }
        };
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<TicketSoporte>>(),
                It.IsAny<FilterDefinition<TicketSoporte>>(),
                It.IsAny<SortDefinition<TicketSoporte>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(tickets);

        _client.DefaultRequestHeaders.Authorization =
            new("Bearer", GenerateAdminToken());

        var response = await _client.GetAsync("/api/Admin/tickets?estado=abierto");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }
}
