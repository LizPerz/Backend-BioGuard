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

public class UsuariosWebIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<UsuarioWeb>> _mockUsuarios;
    private readonly Mock<IMongoCollection<Plan>> _mockPlanes;

    public UsuariosWebIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;
        _mockUsuarios = new Mock<IMongoCollection<UsuarioWeb>>();
        _mockPlanes = new Mock<IMongoCollection<Plan>>();
        _mockDb.Setup(db => db.UsuariosWeb).Returns(_mockUsuarios.Object);
        _mockDb.Setup(db => db.Planes).Returns(_mockPlanes.Object);
    }

    [Fact]
    public async Task MiPerfil_UsuarioExiste_Retorna200()
    {
        var usuario = new UsuarioWeb
        {
            Id = "user123", Nombre = "Juan", ApellidoPaterno = "Perez",
            ApellidoMaterno = "Lopez", Correo = "juan@test.com", PlanId = "plan1"
        };
        var plan = new Plan { Id = "plan1", Nombre = "Premium" };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(usuario);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken("user123"));

        var response = await _client.GetAsync("/api/UsuariosWeb/mi-perfil");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("nombre").GetString().Should().Be("Juan");
    }

    [Fact]
    public async Task MiPerfil_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/UsuariosWeb/mi-perfil");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MiPlan_UsuarioConPlan_Retorna200()
    {
        var usuario = new UsuarioWeb { Id = "user123", PlanId = "plan1" };
        var plan = new Plan
        {
            Id = "plan1", Nombre = "Premium", Precio = 9.99m,
            PrecioMoneda = "USD", LimitePacientes = 1, LimiteCuidadores = 3,
            DiasHistorial = 90, GpsContinuo = true, AiConsole = true,
            Descripcion = "Plan completo"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(usuario);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken("user123"));

        var response = await _client.GetAsync("/api/UsuariosWeb/mi-plan");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("nombre").GetString().Should().Be("Premium");
    }

    [Fact]
    public async Task EditarPerfil_DatosValidos_Retorna200()
    {
        var mockUpdateResult = new Mock<UpdateResult>();
        mockUpdateResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockUsuarios.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpdateResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken("user123"));

        var request = new UpdatePerfilRequest("Juan", "Perez", "Lopez");
        var response = await _client.PutAsJsonAsync("/api/UsuariosWeb/mi-perfil", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EliminarCuenta_SinToken_Retorna401()
    {
        var response = await _client.DeleteAsync("/api/UsuariosWeb/mi-cuenta");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CambiarCorreo_DatosValidos_Retorna200()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);

        var mockUpdateResult = new Mock<UpdateResult>();
        mockUpdateResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpdateResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken("user123"));

        var request = new CambiarCorreoRequest("nuevo@test.com");
        var response = await _client.PutAsJsonAsync("/api/UsuariosWeb/mi-perfil/correo", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Correo actualizado");
    }

    [Fact]
    public async Task CambiarCorreo_CorreoExistente_Retorna400()
    {
        var existingUser = new UsuarioWeb
        {
            Id = "other_user",
            Correo = "ocupado@test.com",
            Activo = true
        };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(existingUser);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken("user123"));

        var request = new CambiarCorreoRequest("ocupado@test.com");
        var response = await _client.PutAsJsonAsync("/api/UsuariosWeb/mi-perfil/correo", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubirFoto_DatosValidos_Retorna200()
    {
        var mockUpdateResult = new Mock<UpdateResult>();
        mockUpdateResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpdateResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken("user123"));

        var request = new SubirFotoRequest("base64fotodata");
        var response = await _client.PutAsJsonAsync("/api/UsuariosWeb/mi-perfil/foto", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Foto actualizada");
    }

    [Fact]
    public async Task CambiarPlan_PlanValido_Retorna200()
    {
        var plan = new Plan { Id = "plan_pro", Nombre = "Pro" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        var mockUpdateResult = new Mock<UpdateResult>();
        mockUpdateResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateDefinition<UsuarioWeb>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpdateResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken("user123"));

        var request = new CambiarPlanRequest("Pro");
        var response = await _client.PutAsJsonAsync("/api/UsuariosWeb/cambiar-plan", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Plan actualizado");
    }

    [Fact]
    public async Task CambiarPlan_PlanNoExiste_Retorna400()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync((Plan?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken("user123"));

        var request = new CambiarPlanRequest("PlanInexistente");
        var response = await _client.PutAsJsonAsync("/api/UsuariosWeb/cambiar-plan", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EliminarCuenta_ConToken_Retorna204()
    {
        var mockDeleteResult = new Mock<DeleteResult>();
        mockDeleteResult.Setup(r => r.DeletedCount).Returns(1);

        _mockDb.Setup(db => db.DeleteManyAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new List<Paciente>());

        _mockDb.Setup(db => db.DeleteManyAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);

        _mockDb.Setup(db => db.DeleteManyAsync(
                It.IsAny<IMongoCollection<Pago>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Pago, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);

        _mockDb.Setup(db => db.DeleteManyAsync(
                It.IsAny<IMongoCollection<LecturaSensor>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<LecturaSensor, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);

        _mockDb.Setup(db => db.DeleteManyAsync(
                It.IsAny<IMongoCollection<EventoMetabolico>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<EventoMetabolico, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);

        _mockDb.Setup(db => db.DeleteManyAsync(
                It.IsAny<IMongoCollection<TrackingGps>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<TrackingGps, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);

        _mockDb.Setup(db => db.DeleteManyAsync(
                It.IsAny<IMongoCollection<Notificacion>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Notificacion, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);

        _mockDb.Setup(db => db.DeleteManyAsync(
                It.IsAny<IMongoCollection<Dispositivo>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Dispositivo, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);

        _mockUsuarios.Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<UsuarioWeb>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeleteResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken("user123"));

        var response = await _client.DeleteAsync("/api/UsuariosWeb/mi-cuenta");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SubirFoto_SinToken_Retorna401()
    {
        var response = await _client.PutAsJsonAsync("/api/UsuariosWeb/mi-perfil/foto", new { FotoBase64 = "data:image/png;base64,abc123" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CambiarPlan_SinToken_Retorna401()
    {
        var response = await _client.PutAsJsonAsync("/api/UsuariosWeb/cambiar-plan", new { PlanNombre = "Premium" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetByEmail_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/UsuariosWeb/by-email/test@test.com");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetByEmail_UsuarioExiste_Retorna200()
    {
        var usuario = new UsuarioWeb
        {
            Id = "user456", Nombre = "Maria", ApellidoPaterno = "Garcia",
            ApellidoMaterno = "Lopez", Correo = "maria@test.com"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(usuario);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken("user123"));

        var response = await _client.GetAsync("/api/UsuariosWeb/by-email/maria@test.com");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("correo").GetString().Should().Be("maria@test.com");
    }

    [Fact]
    public async Task GetByEmail_UsuarioNoExiste_Retorna404()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken("user123"));

        var response = await _client.GetAsync("/api/UsuariosWeb/by-email/inexistente@test.com");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditarPerfil_SinToken_Retorna401()
    {
        var request = new UpdatePerfilRequest("Juan", "Perez", "Lopez");
        var response = await _client.PutAsJsonAsync("/api/UsuariosWeb/mi-perfil", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CambiarCorreo_SinToken_Retorna401()
    {
        var request = new CambiarCorreoRequest("nuevo@test.com");
        var response = await _client.PutAsJsonAsync("/api/UsuariosWeb/mi-perfil/correo", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MiPlan_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/UsuariosWeb/mi-plan");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
