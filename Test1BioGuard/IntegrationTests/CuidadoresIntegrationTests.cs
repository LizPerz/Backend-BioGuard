using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;

namespace Test1BioGuard.IntegrationTests;

public class CuidadoresIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<Cuidador>> _mockCuidadores;
    private readonly Mock<IMongoCollection<Paciente>> _mockPacientes;

    public CuidadoresIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;

        _mockCuidadores = new Mock<IMongoCollection<Cuidador>>();
        _mockPacientes = new Mock<IMongoCollection<Paciente>>();

        _mockDb.Setup(db => db.Cuidadores).Returns(_mockCuidadores.Object);
        _mockDb.Setup(db => db.Pacientes).Returns(_mockPacientes.Object);
    }

    [Fact]
    public async Task Listar_DuenoConCuidadores_Retorna200()
    {
        var cuidadores = new List<Cuidador>
        {
            new()
            {
                Id = "cuid1",
                Nombre = "Maria Lopez",
                Parentesco = "Madre",
                PacienteId = "pac123",
                CodigoAccesoQr = "CU-ABC123",
                UsuarioWebId = "user123"
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(cuidadores);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Cuidadores");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Listar_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/Cuidadores");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_CuidadorExiste_Retorna200()
    {
        var cuidador = new Cuidador
        {
            Id = "cuid123",
            Nombre = "Ana Garcia",
            Parentesco = "Hermana",
            PacienteId = "pac123",
            CodigoAccesoQr = "CU-XYZ789",
            UsuarioWebId = "user123"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(cuidador);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Cuidadores/cuid123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("nombre").GetString().Should().Be("Ana Garcia");
    }

    [Fact]
    public async Task Crear_DatosValidos_Retorna200()
    {
        _mockDb.Setup(db => db.CountDocumentsAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(1L);

        _mockCuidadores.Setup(c => c.InsertOneAsync(
            It.IsAny<Cuidador>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new CrearCuidadorRequest("pac123", "Pedro Lopez", "Padre", "5551234567", "pedro@test.com");
        var response = await _client.PostAsJsonAsync("/api/Cuidadores", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("codigoAccesoQr").GetString().Should().StartWith("CU-");
    }

    [Fact]
    public async Task Eliminar_CuidadorExiste_Retorna204()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(new Cuidador
            {
                Id = "cuid123",
                Nombre = "Ana Garcia",
                PacienteId = "pac123",
                UsuarioWebId = "user123"
            });

        var mockDeleteResult = new Mock<DeleteResult>();
        mockDeleteResult.Setup(r => r.DeletedCount).Returns(1);

        _mockCuidadores.Setup(c => c.DeleteOneAsync(
            It.IsAny<FilterDefinition<Cuidador>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeleteResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.DeleteAsync("/api/Cuidadores/cuid123");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ObtenerQR_CuidadorExiste_RetornaCodigo()
    {
        var cuidador = new Cuidador
        {
            Id = "cuid123",
            Nombre = "Ana Garcia",
            CodigoAccesoQr = "CU-QR456",
            UsuarioWebId = "user123"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(cuidador);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Cuidadores/cuid123/qr");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("codigoAccesoQr").GetString().Should().Be("CU-QR456");
    }

    [Fact]
    public async Task GetByPaciente_PacienteConCuidadores_Retorna200()
    {
        var cuidadores = new List<Cuidador>
        {
            new()
            {
                Id = "cuid1",
                Nombre = "Maria Lopez",
                Parentesco = "Madre",
                PacienteId = "pac123",
                CodigoAccesoQr = "CU-ABC123"
            }
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new Paciente { Id = "pac123", UsuarioWebId = "user123", Nombre = "Juan Perez" });

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(cuidadores);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Cuidadores/by-paciente/pac123");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Disponibles_ConCuidadores_Retorna200()
    {
        var usuarioId = "user123";
        var pacientes = new List<Paciente>
        {
            new() { Id = "pac123", UsuarioWebId = usuarioId, Nombre = "Juan Perez" }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(pacientes);

        _mockDb.Setup(db => db.CountDocumentsAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(2L);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken(usuarioId));

        var response = await _client.GetAsync("/api/Cuidadores/disponibles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("usados").GetInt64().Should().Be(2);
        doc.RootElement.GetProperty("total").GetInt64().Should().Be(3);
        doc.RootElement.GetProperty("disponibles").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task Disponibles_SinPaciente_Retorna0()
    {
        var usuarioId = "user123";

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new List<Paciente>());

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken(usuarioId));

        var response = await _client.GetAsync("/api/Cuidadores/disponibles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("disponibles").GetInt64().Should().Be(0);
        doc.RootElement.GetProperty("total").GetInt64().Should().Be(0);
    }

    [Fact]
    public async Task Editar_DatosValidos_Retorna200()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(new Cuidador
            {
                Id = "cuid123",
                Nombre = "Ana Garcia",
                Parentesco = "Hermana",
                PacienteId = "pac123",
                UsuarioWebId = "user123"
            });

        var mockUpdateResult = new Mock<UpdateResult>();
        mockUpdateResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockCuidadores.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<Cuidador>>(),
                It.IsAny<UpdateDefinition<Cuidador>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpdateResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new ActualizarCuidadorRequest("Maria Garcia", "Tia");
        var response = await _client.PutAsJsonAsync("/api/Cuidadores/cuid123", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Cuidador actualizado");
    }

    [Fact]
    public async Task Editar_CuidadorNoExiste_Retorna404()
    {
        var mockUpdateResult = new Mock<UpdateResult>();
        mockUpdateResult.Setup(r => r.ModifiedCount).Returns(0);

        _mockCuidadores.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<Cuidador>>(),
                It.IsAny<UpdateDefinition<Cuidador>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpdateResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new ActualizarCuidadorRequest("No Existe", "Desconocido");
        var response = await _client.PutAsJsonAsync("/api/Cuidadores/nonexistent", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RegenerarQR_CuidadorExiste_Retorna200()
    {
        var cuidador = new Cuidador
        {
            Id = "cuid123",
            Nombre = "Ana Garcia",
            CodigoAccesoQr = "CU-OLD123",
            UsuarioWebId = "user123"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(cuidador);

        var mockUpdateResult = new Mock<UpdateResult>();
        mockUpdateResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockCuidadores.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<Cuidador>>(),
                It.IsAny<UpdateDefinition<Cuidador>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpdateResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.PostAsync("/api/Cuidadores/cuid123/regenerar-qr", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("codigoAccesoQr").GetString().Should().StartWith("CU-");
        doc.RootElement.GetProperty("message").GetString().Should().Be("QR regenerado");
    }

    [Fact]
    public async Task RegenerarQR_CuidadorNoExiste_Retorna404()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync((Cuidador?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.PostAsync("/api/Cuidadores/nonexistent/regenerar-qr", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
