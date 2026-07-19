using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BioGuard.Api.DTOs;
using BioGuard.Api.Services;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 1: Autenticación y Acceso
/// ENDPOINT WEB + MÓVIL
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService) => _authService = authService;

    // ── Registro ──────────────────────────────────────────────
    // POST /api/Auth/register [WEB]

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterWebRequest request)
    {
        var result = await _authService.RegisterWebAsync(request);
        if (result == null) return BadRequest(new { message = "El correo ya existe o plan inválido" });
        return Ok(result);
    }

    // ── Login ─────────────────────────────────────────────────
    // POST /api/Auth/login-web [WEB]

    [HttpPost("login-web")]
    public async Task<IActionResult> LoginWeb([FromBody] LoginWebRequest request)
    {
        var result = await _authService.LoginWebAsync(request);
        if (result == null) return Unauthorized(new { message = "Credenciales inválidas" });
        return Ok(result);
    }

    // POST /api/Auth/login-google [WEB]

    [HttpPost("login-google")]
    public async Task<IActionResult> LoginGoogle([FromBody] LoginGoogleRequest request)
    {
        var result = await _authService.LoginGoogleAsync(request);
        if (result == null) return Unauthorized(new { message = "Token de Google inválido" });
        return Ok(result);
    }

    // POST /api/Auth/login-codigo [MÓVIL]

    [HttpPost("login-codigo")]
    public async Task<IActionResult> LoginByCodigo([FromBody] LoginCodigoRequest request)
    {
        var result = await _authService.LoginByCodigoAsync(request);
        if (result == null) return NotFound(new { message = "Código no encontrado" });
        return Ok(result);
    }

    // ── 2FA ───────────────────────────────────────────────────
    // POST /api/Auth/2FA/enviar [WEB]

    [HttpPost("2FA/enviar")]
    public async Task<IActionResult> Enviar2FA([FromBody] Enviar2FARequest request)
    {
        var result = await _authService.Enviar2FAAsync(request);
        if (!result) return BadRequest(new { message = "Correo no encontrado o inactivo" });
        return Ok(new { message = "Código enviado al correo" });
    }

    // POST /api/Auth/2FA/verificar [WEB]

    [HttpPost("2FA/verificar")]
    public async Task<IActionResult> Verificar2FA([FromBody] Verificar2FARequest request)
    {
        var result = await _authService.Verificar2FAAsync(request);
        if (result == null) return BadRequest(new { message = "Código inválido o expirado" });
        return Ok(result);
    }

    // ── Refresh Token ──────────────────────────────────────
    // POST /api/Auth/refresh [WEB]

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RefreshTokenAsync(request, ip);
        if (result == null) return Unauthorized(new { message = "Refresh token inválido o expirado" });
        return Ok(result);
    }

    // ── Recuperación de contraseña ────────────────────────────
    // POST /api/Auth/forgot-password [WEB]

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _authService.ForgotPasswordAsync(request);
        if (!result) return BadRequest(new { message = "Correo no encontrado" });
        return Ok(new { message = "Se envió un link de recuperación a tu correo" });
    }

    // POST /api/Auth/reset-password [WEB]

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(request);
        if (!result) return BadRequest(new { message = "Token inválido o expirado" });
        return Ok(new { message = "Contraseña actualizada correctamente" });
    }

    // ── Cambio de contraseña (logueado) ───────────────────────
    // PUT /api/Auth/cambiar-password [WEB]

    [HttpPut("cambiar-password")]
    [Authorize]
    public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var result = await _authService.CambiarPasswordAsync(userId, request);
        if (!result) return BadRequest(new { message = "Password actual incorrecto" });
        return Ok(new { message = "Contraseña actualizada correctamente" });
    }
}
