using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 2 + 7: Usuarios, Planes y Facturación
/// ENDPOINT WEB
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosWebController : ControllerBase
{
    private readonly UsuariosWebService _usuariosWebService;
    private readonly ILogger<UsuariosWebController> _logger;

    public UsuariosWebController(UsuariosWebService usuariosWebService, ILogger<UsuariosWebController> logger)
    {
        _usuariosWebService = usuariosWebService;
        _logger = logger;
    }

    // ── Perfil ────────────────────────────────────────────────

    /// <summary>
    /// GET /api/UsuariosWeb/mi-perfil [WEB]
    /// MÓDULO 2: Perfil completo del usuario + plan
    /// </summary>
    [HttpGet("mi-perfil")]
    public async Task<IActionResult> MiPerfil()
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Getting profile for user {UsuarioId}", usuarioId);
        var usuario = await _usuariosWebService.GetByIdAsync(usuarioId);
        if (usuario == null)
        {
            _logger.LogWarning("User {UsuarioId} not found", usuarioId);
            return NotFound();
        }

        return Ok(new
        {
            usuario.Id,
            usuario.Nombre,
            usuario.ApellidoPaterno,
            usuario.ApellidoMaterno,
            usuario.Correo,
            usuario.FechaRegistro,
            Plan = (await _usuariosWebService.GetPlanAsync(usuarioId))?.Nombre ?? "Sin plan"
        });
    }

    /// <summary>
    /// PUT /api/UsuariosWeb/mi-perfil [WEB]
    /// MÓDULO 2: Editar nombre y apellidos
    /// </summary>
    [HttpPut("mi-perfil")]
    public async Task<IActionResult> EditarPerfil([FromBody] UpdatePerfilRequest request)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Updating profile for user {UsuarioId}", usuarioId);
        var result = await _usuariosWebService.UpdatePerfilAsync(usuarioId, request);
        if (!result)
        {
            _logger.LogWarning("Profile update failed for user {UsuarioId}", usuarioId);
            return NotFound();
        }
        return Ok(new { message = "Perfil actualizado" });
    }

    /// <summary>
    /// PUT /api/UsuariosWeb/mi-perfil/correo [WEB]
    /// MÓDULO 2: Cambiar correo (requiere verificar)
    /// </summary>
    [HttpPut("mi-perfil/correo")]
    public async Task<IActionResult> CambiarCorreo([FromBody] CambiarCorreoRequest request)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Changing email for user {UsuarioId}", usuarioId);
        var result = await _usuariosWebService.CambiarCorreoAsync(usuarioId, request.NuevoCorreo);
        if (!result)
        {
            _logger.LogWarning("Email change failed for user {UsuarioId}, email already registered or invalid", usuarioId);
            return BadRequest(new { message = "Correo ya registrado o inválido" });
        }
        return Ok(new { message = "Correo actualizado" });
    }

    /// <summary>
    /// PUT /api/UsuariosWeb/mi-perfil/foto [WEB]
    /// MÓDULO 2: Subir foto de perfil (base64 o URL)
    /// </summary>
    [HttpPut("mi-perfil/foto")]
    public async Task<IActionResult> SubirFoto([FromBody] SubirFotoRequest request)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Uploading photo for user {UsuarioId}", usuarioId);
        var result = await _usuariosWebService.SubirFotoAsync(usuarioId, request.FotoBase64);
        if (!result)
        {
            _logger.LogWarning("Photo upload failed for user {UsuarioId}", usuarioId);
            return NotFound();
        }
        return Ok(new { message = "Foto actualizada" });
    }

    // ── Plan / Suscripción ────────────────────────────────────

    /// <summary>
    /// GET /api/UsuariosWeb/mi-plan [WEB]
    /// MÓDULO 2: Ver plan actual del usuario
    /// </summary>
    [HttpGet("mi-plan")]
    public async Task<IActionResult> MiPlan()
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Getting plan for user {UsuarioId}", usuarioId);
        var plan = await _usuariosWebService.GetPlanAsync(usuarioId);
        if (plan == null)
        {
            _logger.LogWarning("No plan found for user {UsuarioId}", usuarioId);
            return NotFound();
        }

        return Ok(new PlanResponse(
            plan.Id, plan.Nombre, plan.Precio, plan.PrecioMoneda,
            plan.LimitePacientes, plan.LimiteCuidadores, plan.DiasHistorial,
            plan.GpsContinuo, plan.AiConsole, plan.Descripcion));
    }

    /// <summary>
    /// PUT /api/UsuariosWeb/cambiar-plan [WEB]
    /// MÓDULO 2: Cambiar de plan (Gratis→Familiar→Pro)
    /// </summary>
    [HttpPut("cambiar-plan")]
    public async Task<IActionResult> CambiarPlan([FromBody] CambiarPlanRequest request)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Changing plan to {PlanNombre} for user {UsuarioId}", request.PlanNombre, usuarioId);
        var result = await _usuariosWebService.CambiarPlanAsync(usuarioId, request.PlanNombre);
        if (!result)
        {
            _logger.LogWarning("Plan change failed for user {UsuarioId}, invalid plan {PlanNombre}", usuarioId, request.PlanNombre);
            return BadRequest(new { message = "Plan no válido" });
        }
        return Ok(new { message = "Plan actualizado" });
    }

    // ── Cuenta ────────────────────────────────────────────────

    /// <summary>
    /// GET /api/UsuariosWeb/by-email/{correo} [WEB]
    /// MÓDULO 2: Buscar usuario por correo
    /// </summary>
    [HttpGet("by-email/{correo}")]
    public async Task<IActionResult> GetByEmail(string correo)
    {
        _logger.LogInformation("Looking up user by email {Correo}", correo);
        var usuario = await _usuariosWebService.GetByEmailAsync(correo);
        if (usuario == null)
        {
            _logger.LogWarning("User with email {Correo} not found", correo);
            return NotFound();
        }

        return Ok(new
        {
            usuario.Id,
            usuario.Nombre,
            usuario.ApellidoPaterno,
            usuario.ApellidoMaterno,
            usuario.Correo
        });
    }

    /// <summary>
    /// DELETE /api/UsuariosWeb/mi-cuenta [WEB]
    /// MÓDULO 2: Eliminar cuenta + todos los datos
    /// </summary>
    [HttpDelete("mi-cuenta")]
    public async Task<IActionResult> EliminarCuenta()
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Deleting account for user {UsuarioId}", usuarioId);
        var result = await _usuariosWebService.EliminarCuentaAsync(usuarioId);
        if (!result)
        {
            _logger.LogWarning("Account deletion failed for user {UsuarioId}", usuarioId);
            return NotFound();
        }
        return NoContent();
    }
}

public record SubirFotoRequest(string FotoBase64);
public record CambiarPlanRequest(string PlanNombre);
