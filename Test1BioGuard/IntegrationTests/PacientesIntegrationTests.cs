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

public class PacientesIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<Paciente>> _mockPacientes;
    private readonly Mock<IMongoCollection<LecturaSensor>> _mockLecturas;
    private readonly Mock<IMongoCollection<EventoMetabolico>> _mockEventos;
    private readonly Mock<IMongoCollection<TrackingGps>> _mockTracking;
    private readonly Mock<IMongoCollection<Notificacion>> _mockNotificaciones;
    private readonly Mock<IMongoCollection<Dispositivo>> _mockDispositivos;

    public PacientesIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;

        _mockPacientes = new Mock<IMongoCollection<Paciente>>();
        _mockLecturas = new Mock<IMongoCollection<LecturaSensor>>();
        _mockEventos = new Mock<IMongoCollection<EventoMetabolico>>();
        _mockTracking = new Mock<IMongoCollection<TrackingGps>>();
        _mockNotificaciones = new Mock<IMongoCollection<Notificacion>>();
        _mockDispositivos = new Mock<IMongoCollection<Dispositivo>>();

        _mockDb.Setup(db => db.Pacientes).Returns(_mockPacientes.Object);
        _mockDb.Setup(db => db.LecturasSensores).Returns(_mockLecturas.Object);
        _mockDb.Setup(db => db.EventosMetabolicos).Returns(_mockEventos.Object);
        _mockDb.Setup(db => db.TrackingGps).Returns(_mockTracking.Object);
        _mockDb.Setup(db => db.Notificaciones).Returns(_mockNotificaciones.Object);
        _mockDb.Setup(db => db.Dispositivos).Returns(_mockDispositivos.Object);

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new List<Paciente>
            {
                new()
                {
                    Id = "123456789012345678901234",
                    UsuarioWebId = "user123",
                    Nombre = "Paciente Test"
                }
            });
    }

    [Fact]
    public async Task GetById_PacienteExiste_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var paciente = new Paciente
        {
            Id = pacienteId,
            Nombre = "Juan Perez",
            CodigoAccesoQr = "ABC12345",
            UsuarioWebId = "user123"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(paciente);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Pacientes/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("nombre").GetString().Should().Be("Juan Perez");
    }

    [Fact]
    public async Task GetById_SinToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/Pacientes/123456789012345678901234");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Crear_DatosValidos_Retorna200()
    {
        _mockPacientes.Setup(c => c.InsertOneAsync(
            It.IsAny<Paciente>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new CrearPacienteRequest("Nuevo Paciente");
        var response = await _client.PostAsJsonAsync("/api/Pacientes", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("codigoAccesoQr").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Eliminar_PacienteExiste_Retorna204()
    {
        var pacienteId = "123456789012345678901234";

        var mockDeleteResult = new Mock<DeleteResult>();
        mockDeleteResult.Setup(r => r.DeletedCount).Returns(1);

        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<LecturaSensor>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<LecturaSensor, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<EventoMetabolico>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<EventoMetabolico, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<TrackingGps>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<TrackingGps, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<Notificacion>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Notificacion, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<Dispositivo>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Dispositivo, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockPacientes.Setup(c => c.DeleteOneAsync(
            It.IsAny<FilterDefinition<Paciente>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeleteResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.DeleteAsync($"/api/Pacientes/{pacienteId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ObtenerDispositivo_SinDispositivo_RetornaVinculadoFalse()
    {
        var pacienteId = "123456789012345678901234";

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Dispositivo>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Dispositivo, bool>>>()))
            .ReturnsAsync((Dispositivo?)null);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Pacientes/{pacienteId}/dispositivo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("vinculado").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ObtenerQR_PacienteExiste_RetornaCodigo()
    {
        var pacienteId = "123456789012345678901234";
        var paciente = new Paciente
        {
            Id = pacienteId,
            CodigoAccesoQr = "XYZ98765",
            Nombre = "Paciente QR"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(paciente);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Pacientes/{pacienteId}/qr");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("codigoAccesoQr").GetString().Should().Be("XYZ98765");
    }

    [Fact]
    public async Task MiPaciente_ConPaciente_Retorna200()
    {
        var usuarioId = "user123";
        var pacientes = new List<Paciente>
        {
            new()
            {
                Id = "123456789012345678901234",
                Nombre = "Juan Perez",
                UsuarioWebId = usuarioId,
                CodigoAccesoQr = "ABC12345",
                Biometria = new Biometria { EsDiabetico = false },
                PerfilCompletado = true
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(pacientes);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken(usuarioId));

        var response = await _client.GetAsync("/api/Pacientes/mi-paciente");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("nombre").GetString().Should().Be("Juan Perez");
    }

    [Fact]
    public async Task MiPaciente_SinPaciente_Retorna404()
    {
        var usuarioId = "user123";

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new List<Paciente>());

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken(usuarioId));

        var response = await _client.GetAsync("/api/Pacientes/mi-paciente");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByUsuario_ConPacientes_Retorna200()
    {
        var usuarioId = "user123";
        var pacientes = new List<Paciente>
        {
            new()
            {
                Id = "123456789012345678901234",
                Nombre = "Paciente Uno",
                UsuarioWebId = usuarioId,
                CodigoAccesoQr = "COD12345"
            }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(pacientes);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.GetAsync($"/api/Pacientes/by-usuario/{usuarioId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Editar_NuevosDatos_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockPacientes.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Paciente>>(),
            It.IsAny<UpdateDefinition<Paciente>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new UpdateNombreRequest("Nuevo Nombre");
        var response = await _client.PutAsJsonAsync($"/api/Pacientes/{pacienteId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Paciente actualizado");
    }

    [Fact]
    public async Task UpdateBiometria_DatosValidos_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockPacientes.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Paciente>>(),
            It.IsAny<UpdateDefinition<Paciente>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new UpdateBiometriaRequest(30, 75.5, 175.0, false, false, "Moderada");
        var response = await _client.PutAsJsonAsync($"/api/Pacientes/{pacienteId}/biometria", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Biometría actualizada");
    }

    [Fact]
    public async Task RegenerarQR_PacienteExiste_Retorna200()
    {
        var pacienteId = "123456789012345678901234";
        var paciente = new Paciente
        {
            Id = pacienteId,
            Nombre = "Paciente QR",
            UsuarioWebId = "user123",
            CodigoAccesoQr = "OLD_CODE_123"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(paciente);

        _mockPacientes.Setup(c => c.InsertOneAsync(
            It.IsAny<Paciente>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var response = await _client.PostAsJsonAsync($"/api/Pacientes/{pacienteId}/regenerar-qr", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("QR regenerado");
        doc.RootElement.GetProperty("codigoAccesoQr").GetString().Should().NotBeNullOrEmpty();
    }
}
