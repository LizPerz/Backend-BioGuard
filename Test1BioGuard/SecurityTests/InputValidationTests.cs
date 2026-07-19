using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using BioGuard.Api.DTOs;

namespace Test1BioGuard.SecurityTests;

public class InputValidationTests : IClassFixture<IntegrationTests.CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public InputValidationTests(IntegrationTests.CustomWebApplicationFactory factory)
    {
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
}
