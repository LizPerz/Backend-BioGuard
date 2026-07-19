using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace Test1BioGuard.IntegrationTests;

public class MedicamentosIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<Medicamento>> _mockMedicamentos;

    public MedicamentosIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;
        _mockMedicamentos = new Mock<IMongoCollection<Medicamento>>();
        _mockDb.Setup(db => db.Medicamentos).Returns(_mockMedicamentos.Object);
    }

    [Fact]
    public async Task ObtenerPorPaciente_ConMedicamentos_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var medicamentos = new List<Medicamento>
        {
            new() { Id = "m1", PacienteId = pacienteId, Nombre = "Metformina", Dosis = "500mg", Horario = "8:00", Activo = true }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Medicamento>>(),
                It.IsAny<FilterDefinition<Medicamento>>(),
                It.IsAny<SortDefinition<Medicamento>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(medicamentos);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken(pacienteId));

        var response = await _client.GetAsync($"/api/Medicamentos/by-paciente/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ObtenerPorPaciente_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/Medicamentos/by-paciente/pac123");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Crear_DatosValidos_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var paciente = new Paciente { Id = pacienteId, UsuarioWebId = "user123", Nombre = "Test" };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(paciente);

        _mockMedicamentos.Setup(c => c.InsertOneAsync(
            It.IsAny<Medicamento>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken("user123"));

        var request = new { PacienteId = pacienteId, Nombre = "Metformina", Dosis = "500mg", Horario = "8:00" };
        var response = await _client.PostAsJsonAsync("/api/Medicamentos", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Medicamento creado");
    }

    [Fact]
    public async Task TriggerMedicamento_DatosValidos_Retorna200()
    {
        _mockMedicamentos.Setup(c => c.InsertOneAsync(
            It.IsAny<Medicamento>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken());

        var request = new { PacienteId = "pac123", Nombre = "Insulina", Dosis = "10u", Horario = "12:00" };
        var response = await _client.PostAsJsonAsync("/api/Medicamentos/trigger", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Medicamento registrado por ML");
    }

    [Fact]
    public async Task RegistrarToma_MedicamentoExiste_Retorna200()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockMedicamentos.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<Medicamento>>(),
                It.IsAny<UpdateDefinition<Medicamento>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GeneratePacienteToken("pac123"));

        var response = await _client.PutAsJsonAsync("/api/Medicamentos/m1/toma", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Eliminar_MedicamentoExiste_Retorna204()
    {
        var medicamento = new Medicamento
        {
            Id = "m1", PacienteId = "123456789012345678901234",
            Nombre = "Metformina", Dosis = "500mg"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Medicamento>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Medicamento, bool>>>()))
            .ReturnsAsync(medicamento);

        var paciente = new Paciente { Id = "123456789012345678901234", UsuarioWebId = "user123" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(paciente);

        var mockResult = new Mock<DeleteResult>();
        mockResult.Setup(r => r.DeletedCount).Returns(1);
        _mockMedicamentos.Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Medicamento>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken("user123"));

        var response = await _client.DeleteAsync("/api/Medicamentos/m1");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Editar_MedicamentoExiste_Retorna200()
    {
        var medicamento = new Medicamento
        {
            Id = "m1", PacienteId = "123456789012345678901234",
            Nombre = "Metformina", Dosis = "500mg", Horario = "8:00"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Medicamento>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Medicamento, bool>>>()))
            .ReturnsAsync(medicamento);

        var paciente = new Paciente { Id = "123456789012345678901234", UsuarioWebId = "user123" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(paciente);

        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockMedicamentos.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<Medicamento>>(),
                It.IsAny<UpdateDefinition<Medicamento>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken("user123"));

        var request = new { Nombre = "Metformina XR", Dosis = "1000mg", Horario = "20:00" };
        var response = await _client.PutAsJsonAsync("/api/Medicamentos/m1", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Medicamento actualizado");
    }

    [Fact]
    public async Task CambiarActivo_MedicamentoExiste_Retorna200()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockMedicamentos.Setup(c => c.UpdateOneAsync(
                It.IsAny<FilterDefinition<Medicamento>>(),
                It.IsAny<UpdateDefinition<Medicamento>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken());

        var response = await _client.PutAsJsonAsync("/api/Medicamentos/m1/activo", false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Medicamento desactivado");
    }

    [Fact]
    public async Task GetById_MedicamentoExiste_Retorna200()
    {
        var medicamento = new Medicamento
        {
            Id = "m1", PacienteId = "123456789012345678901234",
            Nombre = "Metformina", Dosis = "500mg", Horario = "8:00",
            Activo = true, FechaCreacion = DateTime.UtcNow
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Medicamento>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Medicamento, bool>>>()))
            .ReturnsAsync(medicamento);

        var paciente = new Paciente { Id = "123456789012345678901234", UsuarioWebId = "user123" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(paciente);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken("user123"));

        var response = await _client.GetAsync("/api/Medicamentos/m1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("nombre").GetString().Should().Be("Metformina");
    }

    [Fact]
    public async Task GetById_MedicamentoNoExiste_Retorna404()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Medicamento>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Medicamento, bool>>>()))
            .ReturnsAsync((Medicamento?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync("/api/Medicamentos/nonexistent");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Editar_MedicamentoNoExiste_Retorna404()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Medicamento>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Medicamento, bool>>>()))
            .ReturnsAsync((Medicamento?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken());

        var request = new { Nombre = "X", Dosis = "X", Horario = "X" };
        var response = await _client.PutAsJsonAsync("/api/Medicamentos/nonexistent", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
