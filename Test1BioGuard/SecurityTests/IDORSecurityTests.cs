using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using Test1BioGuard.IntegrationTests;

namespace Test1BioGuard.SecurityTests;

public class IDORSecurityTests : IClassFixture<IntegrationTests.CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly Mock<IMongoCollection<Paciente>> _mockPacientes;
    private readonly Mock<IMongoCollection<LecturaSensor>> _mockLecturas;
    private readonly Mock<IMongoCollection<EventoMetabolico>> _mockEventos;
    private readonly Mock<IMongoCollection<Alerta>> _mockAlertas;
    private readonly Mock<IMongoCollection<Medicamento>> _mockMedicamentos;
    private readonly Mock<IMongoCollection<TrackingGps>> _mockTracking;
    private readonly Mock<IMongoCollection<Notificacion>> _mockNotificaciones;

    private const string OwnerUserId = "owner_abc123";
    private const string OtherUserId = "other_xyz789";
    private const string PacienteId = "123456789012345678901234";

    public IDORSecurityTests(IntegrationTests.CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;

        _mockPacientes = new Mock<IMongoCollection<Paciente>>();
        _mockLecturas = new Mock<IMongoCollection<LecturaSensor>>();
        _mockEventos = new Mock<IMongoCollection<EventoMetabolico>>();
        _mockAlertas = new Mock<IMongoCollection<Alerta>>();
        _mockMedicamentos = new Mock<IMongoCollection<Medicamento>>();
        _mockTracking = new Mock<IMongoCollection<TrackingGps>>();
        _mockNotificaciones = new Mock<IMongoCollection<Notificacion>>();

        _mockDb.Setup(db => db.Pacientes).Returns(_mockPacientes.Object);
        _mockDb.Setup(db => db.LecturasSensores).Returns(_mockLecturas.Object);
        _mockDb.Setup(db => db.EventosMetabolicos).Returns(_mockEventos.Object);
        _mockDb.Setup(db => db.Alertas).Returns(_mockAlertas.Object);
        _mockDb.Setup(db => db.Medicamentos).Returns(_mockMedicamentos.Object);
        _mockDb.Setup(db => db.TrackingGps).Returns(_mockTracking.Object);
        _mockDb.Setup(db => db.Notificaciones).Returns(_mockNotificaciones.Object);

        SetupPacienteAsOtherUser();
    }

    private void SetupPacienteAsOtherUser()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new Paciente
            {
                Id = PacienteId,
                UsuarioWebId = OwnerUserId,
                Nombre = "Paciente Propietario"
            });

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new List<Paciente>
            {
                new()
                {
                    Id = PacienteId,
                    UsuarioWebId = OwnerUserId,
                    Nombre = "Paciente Propietario"
                }
            });
    }

    private void SetOtherUserToken()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken(OtherUserId));
    }

    // ── Medicamentos IDOR ──────────────────────────────────

    [Fact]
    public async Task Medicamentos_ObtenerPorPaciente_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Medicamentos/by-paciente/{PacienteId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Medicamentos_GetById_OtroUsuario_Retorna403()
    {
        var medicamento = new Medicamento
        {
            Id = "m1", PacienteId = PacienteId, Nombre = "Metformina",
            Dosis = "500mg", Horario = "8:00", Activo = true
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Medicamento>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Medicamento, bool>>>()))
            .ReturnsAsync(medicamento);

        SetOtherUserToken();
        var response = await _client.GetAsync("/api/Medicamentos/m1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Medicamentos_Crear_OtroPaciente_Retorna403()
    {
        SetOtherUserToken();
        var request = new { PacienteId = PacienteId, Nombre = "Insulina", Dosis = "10u", Horario = "12:00" };
        var response = await _client.PostAsJsonAsync("/api/Medicamentos", request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Medicamentos_Editar_OtroUsuario_Retorna403()
    {
        var medicamento = new Medicamento
        {
            Id = "m1", PacienteId = PacienteId, Nombre = "Metformina",
            Dosis = "500mg", Horario = "8:00", Activo = true
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Medicamento>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Medicamento, bool>>>()))
            .ReturnsAsync(medicamento);

        SetOtherUserToken();
        var request = new { Nombre = "Metformina XR", Dosis = "1000mg", Horario = "20:00" };
        var response = await _client.PutAsJsonAsync("/api/Medicamentos/m1", request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Medicamentos_Eliminar_OtroUsuario_Retorna403()
    {
        var medicamento = new Medicamento
        {
            Id = "m1", PacienteId = PacienteId, Nombre = "Metformina",
            Dosis = "500mg", Horario = "8:00", Activo = true
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Medicamento>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Medicamento, bool>>>()))
            .ReturnsAsync(medicamento);

        SetOtherUserToken();
        var response = await _client.DeleteAsync("/api/Medicamentos/m1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Alertas IDOR ───────────────────────────────────────

    [Fact]
    public async Task Alertas_ObtenerPorPaciente_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Alertas/by-paciente/{PacienteId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Alertas_ObtenerPendientes_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Alertas/pendientes/{PacienteId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Alertas_GetById_OtroUsuario_Retorna403()
    {
        var alerta = new Alerta
        {
            Id = "a1", PacienteId = PacienteId, Tipo = "glucosa",
            Nivel = "critico", Titulo = "Alerta", Mensaje = "Test", Atendida = false
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Alerta>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Alerta, bool>>>()))
            .ReturnsAsync(alerta);

        SetOtherUserToken();
        var response = await _client.GetAsync("/api/Alertas/a1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Alertas_Eliminar_OtroUsuario_Retorna403()
    {
        var alerta = new Alerta
        {
            Id = "a1", PacienteId = PacienteId, Tipo = "glucosa",
            Nivel = "critico", Titulo = "Alerta", Mensaje = "Test"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Alerta>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Alerta, bool>>>()))
            .ReturnsAsync(alerta);

        SetOtherUserToken();
        var response = await _client.DeleteAsync("/api/Alertas/a1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Reportes IDOR ──────────────────────────────────────

    [Fact]
    public async Task Reportes_Resumen_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Reportes/resumen/{PacienteId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reportes_HistorialAlertas_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Reportes/historial-alertas/{PacienteId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reportes_HistorialEventos_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Reportes/historial-eventos/{PacienteId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reportes_HistorialMedicamentos_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Reportes/historial-medicamentos/{PacienteId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reportes_HistorialLecturas_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Reportes/historial-lecturas/{PacienteId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Pacientes IDOR ─────────────────────────────────────
    // NOTE: PacientesController.VerifyPacienteOwnership calls GetAllByUsuarioAsync(userId)
    // which uses FindToListAsync with a predicate filter. Moq cannot distinguish between
    // different lambda expressions, so IDOR tests for Pacientes are limited.
    // GetByUsuario and MiPaciente are tested below. For GetById/Editar/Eliminar/Dispositivo,
    // the mock returns the same paciente regardless of the user, so those IDOR tests
    // are best done with an integration database.

    [Fact]
    public async Task Pacientes_GetByUsuario_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Pacientes/by-usuario/{OwnerUserId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Pacientes_MiPaciente_SinPaciente_Retorna404()
    {
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new List<Paciente>());

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestTokenHelper.GenerateDuenoToken(OtherUserId));

        var response = await _client.GetAsync("/api/Pacientes/mi-paciente");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Sensores IDOR ──────────────────────────────────────
    // SensoresController.VerifyPacienteOwnership uses GetByIdAsync which we can mock per-user.

    [Fact]
    public async Task Sensores_ObtenerLecturas_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Sensores/lecturas/{PacienteId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sensores_ObtenerLecturasRango_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Sensores/lecturas/{PacienteId}/rango?desde=2024-01-01T00:00:00Z&hasta=2024-12-31T23:59:59Z");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sensores_Estadisticas_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Sensores/estadisticas/{PacienteId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sensores_ObtenerEventos_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Sensores/eventos/{PacienteId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sensores_ResumenEventos_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Sensores/eventos/{PacienteId}/resumen");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sensores_TrackingActual_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Sensores/tracking/{PacienteId}/actual");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sensores_TrackingRuta_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Sensores/tracking/{PacienteId}/ruta?desde=2024-01-01T00:00:00Z&hasta=2024-12-31T23:59:59Z");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sensores_ExportarPDF_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Sensores/lecturas/{PacienteId}/exportar-pdf");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sensores_Tendencia_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Sensores/estadisticas/{PacienteId}/tendencia?periodo=diario");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Notificaciones IDOR ────────────────────────────────

    [Fact]
    public async Task Notificaciones_ObtenerPorPaciente_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Notificaciones/by-paciente/{PacienteId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Notificaciones_ObtenerPorUsuario_OtroUsuario_Retorna403()
    {
        SetOtherUserToken();
        var response = await _client.GetAsync($"/api/Notificaciones/by-usuario/{OwnerUserId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── TriggerMedicamento IDOR ────────────────────────────

    [Fact]
    public async Task Medicamentos_Trigger_OtroPaciente_Retorna403()
    {
        SetOtherUserToken();
        var request = new { PacienteId = PacienteId, Nombre = "Ibuprofeno", Dosis = "200mg", Horario = "8:00" };
        var response = await _client.PostAsJsonAsync("/api/Medicamentos/trigger", request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
