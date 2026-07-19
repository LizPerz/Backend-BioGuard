using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using BioGuard.Api.DTOs;

namespace Test1BioGuard.SecurityTests;

public class InputValidationTests : IClassFixture<IntegrationTests.CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTests.CustomWebApplicationFactory factory;

    public InputValidationTests(IntegrationTests.CustomWebApplicationFactory factory)
    {
        this.factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_CorreoInvalido_Retorna400()
    {
        var request = new RegisterWebRequest(
            "Juan", "Perez", "Lopez", "correo-invalido", "Password123!", "Premium");

        var response = await _client.PostAsJsonAsync("/api/Auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_PasswordCorta_Retorna400()
    {
        var request = new RegisterWebRequest(
            "Juan", "Perez", "Lopez", "juan@test.com", "123", "Premium");

        var response = await _client.PostAsJsonAsync("/api/Auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_NombreVacio_Retorna400()
    {
        var request = new RegisterWebRequest(
            "", "Perez", "Lopez", "juan@test.com", "Password123!", "Premium");

        var response = await _client.PostAsJsonAsync("/api/Auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_CorreoInvalido_Retorna400()
    {
        var request = new LoginWebRequest("correo-invalido", "Password123!");

        var response = await _client.PostAsJsonAsync("/api/Auth/login-web", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_CampoVacio_Retorna400()
    {
        var request = new LoginWebRequest("", "");

        var response = await _client.PostAsJsonAsync("/api/Auth/login-web", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForgotPassword_CorreoInvalido_Retorna400()
    {
        var request = new ForgotPasswordRequest("correo-invalido");

        var response = await _client.PostAsJsonAsync("/api/Auth/forgot-password", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_TokenVacio_Retorna400()
    {
        var request = new ResetPasswordRequest("", "NewPassword123!");

        var response = await _client.PostAsJsonAsync("/api/Auth/reset-password", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CambiarPassword_SinAuth_Retorna401()
    {
        var request = new CambiarPasswordRequest("OldPass123!", "NewPass123!");

        var response = await _client.PutAsJsonAsync("/api/Auth/cambiar-password", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Medicamentos DTO Validation ────────────────────────

    [Fact]
    public async Task CrearMedicamento_NombreVacio_Retorna400()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                IntegrationTests.TestTokenHelper.GenerateDuenoToken());

        var request = new { PacienteId = "pac123", Nombre = "", Dosis = "500mg", Horario = "8:00" };

        var response = await client.PostAsJsonAsync("/api/Medicamentos", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CrearMedicamento_DosisVacia_Retorna400()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                IntegrationTests.TestTokenHelper.GenerateDuenoToken());

        var request = new { PacienteId = "pac123", Nombre = "Metformina", Dosis = "", Horario = "8:00" };

        var response = await client.PostAsJsonAsync("/api/Medicamentos", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CrearMedicamento_PacienteIdVacio_Retorna400()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                IntegrationTests.TestTokenHelper.GenerateDuenoToken());

        var request = new { PacienteId = "", Nombre = "Metformina", Dosis = "500mg", Horario = "8:00" };

        var response = await client.PostAsJsonAsync("/api/Medicamentos", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CrearMedicamento_HorarioVacio_Retorna400()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                IntegrationTests.TestTokenHelper.GenerateDuenoToken());

        var request = new { PacienteId = "pac123", Nombre = "Metformina", Dosis = "500mg", Horario = "" };

        var response = await client.PostAsJsonAsync("/api/Medicamentos", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Alertas DTO Validation ─────────────────────────────

    [Fact]
    public async Task CrearAlerta_PacienteIdVacio_Retorna400()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                IntegrationTests.TestTokenHelper.GenerateDuenoToken());

        var request = new
        {
            PacienteId = "", Tipo = "glucosa", Nivel = "critico",
            Titulo = "Alerta", Mensaje = "Test"
        };

        var response = await client.PostAsJsonAsync("/api/Alertas", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CrearAlerta_TituloVacio_Retorna400()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                IntegrationTests.TestTokenHelper.GenerateDuenoToken());

        var request = new
        {
            PacienteId = "pac123", Tipo = "glucosa", Nivel = "critico",
            Titulo = "", Mensaje = "Test"
        };

        var response = await client.PostAsJsonAsync("/api/Alertas", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CrearAlerta_NivelVacio_Retorna400()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                IntegrationTests.TestTokenHelper.GenerateDuenoToken());

        var request = new
        {
            PacienteId = "pac123", Tipo = "glucosa", Nivel = "",
            Titulo = "Alerta", Mensaje = "Test"
        };

        var response = await client.PostAsJsonAsync("/api/Alertas", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CrearAlerta_MensajeVacio_Retorna400()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                IntegrationTests.TestTokenHelper.GenerateDuenoToken());

        var request = new
        {
            PacienteId = "pac123", Tipo = "glucosa", Nivel = "critico",
            Titulo = "Alerta", Mensaje = ""
        };

        var response = await client.PostAsJsonAsync("/api/Alertas", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
