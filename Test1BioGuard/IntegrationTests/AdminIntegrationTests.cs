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
    private readonly Mock<IMongoCollection<Plan>> _mockPlanes;
    private readonly Mock<IMongoCollection<TicketSoporte>> _mockTickets;
    private readonly Mock<IMongoCollection<Auditoria>> _mockAuditoria;

    public AdminIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;

        _mockUsuarios = new Mock<IMongoCollection<UsuarioWeb>>();
        _mockPlanes = new Mock<IMongoCollection<Plan>>();
        _mockTickets = new Mock<IMongoCollection<TicketSoporte>>();
        _mockAuditoria = new Mock<IMongoCollection<Auditoria>>();

        _mockDb.Setup(db => db.UsuariosWeb).Returns(_mockUsuarios.Object);
        _mockDb.Setup(db => db.Planes).Returns(_mockPlanes.Object);
        _mockDb.Setup(db => db.TicketsSoporte).Returns(_mockTickets.Object);
        _mockDb.Setup(db => db.Auditoria).Returns(_mockAuditoria.Object);
    }

    private void SetAdminAuth() => _client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
            TestTokenHelper.GenerateToken("admin1", "admin"));

    private static Mock<UpdateResult> MockUpdateResult(long modifiedCount = 1)
    {
        var m = new Mock<UpdateResult>();
        m.Setup(r => r.ModifiedCount).Returns(modifiedCount);
        return m;
    }

    [Fact]
    public async Task ListarUsuarios_ConDatos_Retorna200()
    {
        var usuarios = new List<UsuarioWeb>
        {
            new() { Id = "u1", Correo = "a@test.com", Nombre = "A", ApellidoPaterno = "X", Activo = true, PlanId = "p1", FechaRegistro = DateTime.UtcNow }
        };
        var planes = new List<Plan> { new() { Id = "p1", Nombre = "Premium" } };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<FilterDefinition<UsuarioWeb>>(),
                It.IsAny<SortDefinition<UsuarioWeb>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(usuarios);

        _mockDb.Setup(db => db.CountDocumentsAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(1L);

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<FilterDefinition<Plan>>(),
                It.IsAny<SortDefinition<Plan>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(planes);

        _mockAuditoria.Setup(c => c.InsertOneAsync(
                It.IsAny<Auditoria>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SetAdminAuth();
        var response = await _client.GetAsync("/api/Admin/usuarios");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("usuarios").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("total").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task ListarUsuarios_SinDatos_Retorna200()
    {
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<FilterDefinition<UsuarioWeb>>(),
                It.IsAny<SortDefinition<UsuarioWeb>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<UsuarioWeb>());

        _mockDb.Setup(db => db.CountDocumentsAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(0L);

        SetAdminAuth();
        var response = await _client.GetAsync("/api/Admin/usuarios");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUsuario_Existe_Retorna200()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(new UsuarioWeb { Id = "u1", Correo = "a@test.com", Nombre = "A", ApellidoPaterno = "X", Activo = true, FechaRegistro = DateTime.UtcNow });

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync((Plan?)null);

        SetAdminAuth();
        var response = await _client.GetAsync("/api/Admin/usuarios/u1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("correo").GetString().Should().Be("a@test.com");
    }

    [Fact]
    public async Task GetUsuario_NoExiste_Retorna404()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);

        SetAdminAuth();
        var response = await _client.GetAsync("/api/Admin/usuarios/nonexistent");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PausarUsuario_ConMotivo_Retorna200()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(new UsuarioWeb { Id = "u1", Activo = true });

        _mockUsuarios.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockUpdateResult().Object);

        _mockAuditoria.Setup(c => c.InsertOneAsync(
                It.IsAny<Auditoria>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SetAdminAuth();
        var response = await _client.PutAsJsonAsync("/api/Admin/usuarios/u1/pausar", new { pausar = true, motivo = "Prueba" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Cuenta pausada");
    }

    [Fact]
    public async Task PausarUsuario_SinMotivo_Retorna400()
    {
        SetAdminAuth();
        var response = await _client.PutAsJsonAsync("/api/Admin/usuarios/u1/pausar", new { pausar = true, motivo = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PausarUsuario_Reactivar_Retorna200()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(new UsuarioWeb { Id = "u1", Activo = false });

        _mockUsuarios.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockUpdateResult().Object);

        _mockAuditoria.Setup(c => c.InsertOneAsync(
                It.IsAny<Auditoria>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SetAdminAuth();
        var response = await _client.PutAsJsonAsync("/api/Admin/usuarios/u1/pausar", new { pausar = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Cuenta reactivada");
    }

    [Fact]
    public async Task PausarUsuario_NoExiste_Retorna404()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);

        SetAdminAuth();
        var response = await _client.PutAsJsonAsync("/api/Admin/usuarios/u1/pausar", new { pausar = true, motivo = "Testing" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListarTickets_ConDatos_Retorna200()
    {
        var tickets = new List<TicketSoporte>
        {
            new() { Id = "t1", UsuarioId = "u1", Asunto = "Problema", Estado = "abierto", FechaCreacion = DateTime.UtcNow }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<TicketSoporte>>(),
                It.IsAny<FilterDefinition<TicketSoporte>>(),
                It.IsAny<SortDefinition<TicketSoporte>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(tickets);

        SetAdminAuth();
        var response = await _client.GetAsync("/api/Admin/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ListarTickets_SinDatos_Retorna200()
    {
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<TicketSoporte>>(),
                It.IsAny<FilterDefinition<TicketSoporte>>(),
                It.IsAny<SortDefinition<TicketSoporte>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<TicketSoporte>());

        SetAdminAuth();
        var response = await _client.GetAsync("/api/Admin/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
            .ReturnsAsync(3L);

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(new List<Plan> { new() { Id = "p1", Nombre = "Premium", Activo = true } });

        _mockAuditoria.Setup(c => c.InsertOneAsync(
                It.IsAny<Auditoria>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SetAdminAuth();
        var response = await _client.GetAsync("/api/Admin/metricas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("usuariosActivos", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("pacientesActivos", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("alertasCriticasHoy", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("distribucionPlanes", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Admin_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/Admin/usuarios");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_NoEsAdmin_Retorna403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken("pac123"));

        var response = await _client.GetAsync("/api/Admin/usuarios");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_EsDueno_Retorna403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateToken("dueno123", "dueno"));

        var response = await _client.GetAsync("/api/Admin/usuarios");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SeedAll_SinToken_Retorna401()
    {
        var response = await _client.PostAsync("/api/Seed/seed-all", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SeedAll_ConDuenoToken_Retorna403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateToken("dueno123", "dueno"));

        var response = await _client.PostAsync("/api/Seed/seed-all", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SeedAll_ConAdminToken_Retorna200YNoExponePassword()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(new Plan { Id = "plan1", Nombre = "Gratis" });

        _mockUsuarios.Setup(c => c.InsertOneAsync(
                It.IsAny<UsuarioWeb>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        SetAdminAuth();
        var response = await _client.PostAsync("/api/Seed/seed-all", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("message", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("password", out _).Should().BeFalse();
    }

    [Fact]
    public async Task MigratePasswords_SinToken_Retorna401()
    {
        var response = await _client.PostAsync("/api/Seed/migrate-passwords", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MigratePasswords_ConDuenoToken_Retorna403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateToken("dueno123", "dueno"));

        var response = await _client.PostAsync("/api/Seed/migrate-passwords", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
