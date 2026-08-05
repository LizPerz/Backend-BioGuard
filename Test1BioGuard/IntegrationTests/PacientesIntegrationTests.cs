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
    private readonly Mock<IMongoCollection<Medicamento>> _mockMedicamentos;
    private readonly Mock<IMongoCollection<Alerta>> _mockAlertas;
    private readonly Mock<IMongoCollection<Cuidador>> _mockCuidadores;

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
        _mockMedicamentos = new Mock<IMongoCollection<Medicamento>>();
        _mockAlertas = new Mock<IMongoCollection<Alerta>>();
        _mockCuidadores = new Mock<IMongoCollection<Cuidador>>();

        _mockDb.Setup(db => db.Pacientes).Returns(_mockPacientes.Object);
        _mockDb.Setup(db => db.LecturasSensores).Returns(_mockLecturas.Object);
        _mockDb.Setup(db => db.EventosMetabolicos).Returns(_mockEventos.Object);
        _mockDb.Setup(db => db.TrackingGps).Returns(_mockTracking.Object);
        _mockDb.Setup(db => db.Notificaciones).Returns(_mockNotificaciones.Object);
        _mockDb.Setup(db => db.Dispositivos).Returns(_mockDispositivos.Object);
        _mockDb.Setup(db => db.Medicamentos).Returns(_mockMedicamentos.Object);
        _mockDb.Setup(db => db.Alertas).Returns(_mockAlertas.Object);
        _mockDb.Setup(db => db.Cuidadores).Returns(_mockCuidadores.Object);

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

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(new UsuarioWeb { Id = "user123", PlanId = "plan123" });

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(new Plan { Id = "plan123", Nombre = "Familiar", LimitePacientes = 3, LimiteCuidadores = 3, DiasHistorial = 30 });
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

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new Paciente { Id = pacienteId, UsuarioWebId = "user123" });

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
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<Medicamento>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Medicamento, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<Alerta>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Alerta, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<Cuidador>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
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
            Nombre = "Paciente QR",
            UsuarioWebId = "user123"
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

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new Paciente { Id = pacienteId, UsuarioWebId = "user123" });

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

    [Fact]
    public async Task Crear_SinToken_Retorna401()
    {
        var request = new CrearPacienteRequest("Nuevo");
        var response = await _client.PostAsJsonAsync("/api/Pacientes", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Editar_SinToken_Retorna401()
    {
        var request = new UpdateNombreRequest("Nombre");
        var response = await _client.PutAsJsonAsync("/api/Pacientes/123456789012345678901234", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Crear_ConDatosBiometricos_Retorna200()
    {
        _mockPacientes.Setup(c => c.InsertOneAsync(
                It.IsAny<Paciente>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new CrearPacienteRequest(
            Nombre: "Ana García",
            FechaNacimiento: new DateTime(1990, 3, 10),
            Edad: 34,
            PesoKg: 60.5,
            EstaturaCm: 165.0,
            Sexo: "F",
            EsDiabetico: true,
            FamiliaresDiabetes: true,
            ActividadFisica: "Moderada");

        var response = await _client.PostAsJsonAsync("/api/Pacientes", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("codigoAccesoQr").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Crear_FechaNacimientoFutura_Retorna400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new CrearPacienteRequest(
            Nombre: "Paciente Futuro",
            FechaNacimiento: DateTime.UtcNow.AddYears(1));

        var response = await _client.PostAsJsonAsync("/api/Pacientes", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Contain("fecha de nacimiento");
    }

    [Fact]
    public async Task MiPaciente_DevuelveDatosBiometricos()
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
                FechaNacimiento = new DateTime(1995, 5, 15),
                Biometria = new Biometria
                {
                    Edad = 29,
                    PesoKg = 75.5,
                    EstaturaCm = 178.0,
                    EsDiabetico = true,
                    FamiliaresDiabetes = true,
                    ActividadFisica = "Intensa",
                    Sexo = "M"
                },
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
        doc.RootElement.GetProperty("fechaNacimiento").GetDateTime().Should().Be(new DateTime(1995, 5, 15));
        doc.RootElement.GetProperty("pesoKg").GetDouble().Should().Be(75.5);
        doc.RootElement.GetProperty("estaturaCm").GetDouble().Should().Be(178.0);
        doc.RootElement.GetProperty("sexo").GetString().Should().Be("M");
        doc.RootElement.GetProperty("esDiabetico").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("familiaresDiabetes").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("actividadFisica").GetString().Should().Be("Intensa");
        doc.RootElement.GetProperty("codigoAccesoQr").GetString().Should().Be("ABC12345");
    }

    [Fact]
    public async Task UpdateBiometria_ConFechaNacimientoYSexo_Retorna200()
    {
        var pacienteId = "123456789012345678901234";

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new Paciente { Id = pacienteId, UsuarioWebId = "user123" });

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

        var request = new UpdateBiometriaRequest(
            Edad: 30, PesoKg: 75.5, EstaturaCm: 175.0,
            EsDiabetico: false, FamiliaresDiabetes: false, ActividadFisica: "Moderada",
            FechaNacimiento: new DateTime(1995, 5, 15), Sexo: "M");
        var response = await _client.PutAsJsonAsync($"/api/Pacientes/{pacienteId}/biometria", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Be("Biometría actualizada");
    }

    [Fact]
    public async Task UpdateBiometria_FechaNacimientoFutura_Retorna400()
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

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new Paciente { Id = pacienteId, UsuarioWebId = "user123" });

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokenHelper.GenerateDuenoToken());

        var request = new UpdateBiometriaRequest(
            Edad: 30, PesoKg: 75.5, EstaturaCm: 175.0,
            EsDiabetico: false, FamiliaresDiabetes: false, ActividadFisica: "Moderada",
            FechaNacimiento: DateTime.UtcNow.AddYears(1), Sexo: "M");
        var response = await _client.PutAsJsonAsync($"/api/Pacientes/{pacienteId}/biometria", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("message").GetString().Should().Contain("fecha de nacimiento");
    }
}
