using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;
using BioGuard.Api.Config;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 3: Gestión del Paciente (1 por cuenta)
/// ENDPOINT WEB + MÓVIL
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PacientesController : ControllerBase
{
    private readonly PacienteService _pacienteService;
    private readonly SensorService _sensorService;
    private readonly DispositivoService _dispositivoService;
    private readonly IMongoDbContext _db;
    private readonly AuditoriaService _auditoriaService;
    private readonly ILogger<PacientesController> _logger;

    public PacientesController(PacienteService pacienteService, SensorService sensorService,
        DispositivoService dispositivoService, IMongoDbContext db, AuditoriaService auditoriaService, ILogger<PacientesController> logger)
    {
        _pacienteService = pacienteService;
        _sensorService = sensorService;
        _dispositivoService = dispositivoService;
        _db = db;
        _auditoriaService = auditoriaService;
        _logger = logger;
    }

    // ── Consulta ──────────────────────────────────────────────

    /// <summary>
    /// GET /api/Pacientes/mi-paciente [WEB]
    /// MÓDULO 3: Obtener paciente del usuario logueado
    /// </summary>
    [HttpGet("mi-paciente")]
    public async Task<IActionResult> MiPaciente()
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Fetching mi-paciente for user: {UserId}", usuarioId);
        var pacientes = await _pacienteService.GetAllByUsuarioAsync(usuarioId);
        var paciente = pacientes.FirstOrDefault();
        if (paciente == null)
        {
            _logger.LogWarning("No patient found for user: {UserId}", usuarioId);
            return NotFound(new { message = "No tiene paciente registrado" });
        }

        return Ok(new PacienteResponse(
            paciente.Id, paciente.Nombre, paciente.Biometria?.EsDiabetico ?? false,
            paciente.PerfilCompletado));
    }

    /// <summary>
    /// GET /api/Pacientes/{id} [WEB + MÓVIL]
    /// MÓDULO 3: Obtener paciente por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(id, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed for user: {UserId}, paciente: {PacienteId}, role: {Role}", usuarioId, id, role);
            return Forbid();
        }

        _logger.LogInformation("Fetching paciente by ID: {PacienteId}", id);
        var paciente = await _pacienteService.GetByIdAsync(id);
        if (paciente == null)
        {
            _logger.LogWarning("Paciente not found: {PacienteId}", id);
            return NotFound();
        }

        return Ok(new PacienteResponse(
            paciente.Id, paciente.Nombre, paciente.Biometria?.EsDiabetico ?? false,
            paciente.PerfilCompletado));
    }

    /// <summary>
    /// GET /api/Pacientes/by-usuario/{usuarioWebId} [WEB]
    /// MÓDULO 3: Listar pacientes de un usuario
    /// </summary>
    [HttpGet("by-usuario/{usuarioWebId}")]
    public async Task<IActionResult> GetByUsuario(string usuarioWebId)
    {
        var currentUserId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

        if (role != "dueno" || currentUserId != usuarioWebId)
        {
            _logger.LogWarning("Ownership check failed listing patients - current user: {UserId}, requested user: {RequestedUserId}, role: {Role}", currentUserId, usuarioWebId, role);
            return Forbid();
        }

        _logger.LogInformation("Listing patients for user: {UserId}", usuarioWebId);
        var pacientes = await _pacienteService.GetAllByUsuarioAsync(usuarioWebId);
        var response = pacientes.Select(p => new PacienteResponse(
            p.Id, p.Nombre, p.Biometria?.EsDiabetico ?? false,
            p.PerfilCompletado)).ToList();
        return Ok(response);
    }

    // ── Alta / Edición ────────────────────────────────────────

    /// <summary>
    /// POST /api/Pacientes [WEB]
    /// MÓDULO 3: Crear paciente + generar QR y código
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Crear([FromBody] CrearPacienteRequest request)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Creating paciente for user: {UserId}, name: {Nombre}", usuarioId, request.Nombre);
        var codigo = await _pacienteService.CrearPacienteAsync(usuarioId, request.Nombre);
        _logger.LogInformation("Paciente created successfully for user: {UserId}", usuarioId);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _auditoriaService.RegistrarAsync(usuarioId, "crear", "pacientes", codigo, ip);
        return Ok(new { message = "Paciente creado", CodigoAccesoQr = codigo });
    }

    /// <summary>
    /// PUT /api/Pacientes/{id} [WEB]
    /// MÓDULO 3: Editar nombre del paciente
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Editar(string id, [FromBody] UpdateNombreRequest request)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(id, usuarioId, "dueno"))
        {
            _logger.LogWarning("Ownership check failed editing paciente - user: {UserId}, paciente: {PacienteId}", usuarioId, id);
            return Forbid();
        }

        _logger.LogInformation("Editing paciente: {PacienteId}, new name: {Nombre}", id, request.Nombre);
        var result = await _pacienteService.UpdateNombreAsync(id, request.Nombre);
        if (!result)
        {
            _logger.LogWarning("Paciente not found for edit: {PacienteId}", id);
            return NotFound();
        }
        _logger.LogInformation("Paciente edited successfully: {PacienteId}", id);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _auditoriaService.RegistrarAsync(usuarioId, "editar", "pacientes", id, ip);
        return Ok(new { message = "Paciente actualizado" });
    }

    /// <summary>
    /// DELETE /api/Pacientes/{id} [WEB]
    /// MÓDULO 3: Desvincular paciente + borrar datos
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Eliminar(string id)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(id, usuarioId, "dueno"))
        {
            _logger.LogWarning("Ownership check failed deleting paciente - user: {UserId}, paciente: {PacienteId}", usuarioId, id);
            return Forbid();
        }

        _logger.LogInformation("Deleting paciente: {PacienteId}", id);
        var result = await _pacienteService.EliminarAsync(id);
        if (!result)
        {
            _logger.LogWarning("Paciente not found for deletion: {PacienteId}", id);
            return NotFound();
        }
        _logger.LogInformation("Paciente deleted successfully: {PacienteId}", id);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _auditoriaService.RegistrarAsync(usuarioId, "eliminar", "pacientes", id, ip);
        return NoContent();
    }

    // ── Calibración (Móvil) ───────────────────────────────────

    /// <summary>
    /// PUT /api/Pacientes/{id}/biometria [MÓVIL]
    /// MÓDULO 3: Guardar fecha_nacimiento, sexo, peso, estatura, actividad, diabetes
    /// </summary>
    [HttpPut("{id}/biometria")]
    public async Task<IActionResult> UpdateBiometria(string id, [FromBody] UpdateBiometriaRequest request)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(id, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed updating biometria - user: {UserId}, paciente: {PacienteId}", usuarioId, id);
            return Forbid();
        }

        _logger.LogInformation("Updating biometria for paciente: {PacienteId}", id);
        await _pacienteService.UpdateBiometriaAsync(id, request);
        _logger.LogInformation("Biometria updated for paciente: {PacienteId}", id);
        return Ok(new { message = "Biometría actualizada" });
    }

    // ── QR y Vinculación ──────────────────────────────────────

    /// <summary>
    /// GET /api/Pacientes/{id}/qr [WEB]
    /// MÓDULO 3: Retornar imagen QR + código alfanumérico
    /// </summary>
    [HttpGet("{id}/qr")]
    public async Task<IActionResult> ObtenerQR(string id)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(id, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching QR - user: {UserId}, paciente: {PacienteId}", usuarioId, id);
            return Forbid();
        }

        _logger.LogInformation("Fetching QR for paciente: {PacienteId}", id);
        var paciente = await _pacienteService.GetByIdAsync(id);
        if (paciente == null)
        {
            _logger.LogWarning("Paciente not found for QR: {PacienteId}", id);
            return NotFound();
        }
        return Ok(new { CodigoAccesoQr = paciente.CodigoAccesoQr });
    }

    /// <summary>
    /// POST /api/Pacientes/{id}/regenerar-qr [WEB]
    /// MÓDULO 3: Generar nuevo código (revoca el anterior)
    /// </summary>
    [HttpPost("{id}/regenerar-qr")]
    public async Task<IActionResult> RegenerarQR(string id)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(id, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed regenerating QR - user: {UserId}, paciente: {PacienteId}", usuarioId, id);
            return Forbid();
        }

        _logger.LogInformation("Regenerating QR for paciente: {PacienteId}", id);
        var paciente = await _pacienteService.GetByIdAsync(id);
        if (paciente == null)
        {
            _logger.LogWarning("Paciente not found for QR regen: {PacienteId}", id);
            return NotFound();
        }

        var codigo = await _pacienteService.CrearPacienteAsync(paciente.UsuarioWebId, paciente.Nombre);
        _logger.LogInformation("QR regenerated for paciente: {PacienteId}", id);
        return Ok(new { CodigoAccesoQr = codigo, message = "QR regenerado" });
    }

    // ── Dispositivo ───────────────────────────────────────────

    /// <summary>
    /// GET /api/Pacientes/{id}/dispositivo [WEB + MÓVIL]
    /// MÓDULO 3: Ver si tiene WearOS vinculado y estado
    /// </summary>
    [HttpGet("{id}/dispositivo")]
    public async Task<IActionResult> ObtenerDispositivo(string id)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(id, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching device - user: {UserId}, paciente: {PacienteId}", usuarioId, id);
            return Forbid();
        }

        _logger.LogInformation("Fetching device for paciente: {PacienteId}", id);
        var dispositivo = await _dispositivoService.ObtenerPorPacienteAsync(id);
        if (dispositivo == null) return Ok(new { Vinculado = false });
        return Ok(new
        {
            Vinculado = true,
            dispositivo.NombreDispositivo,
            dispositivo.MacAddress,
            dispositivo.Conectado,
            dispositivo.FechaVinculacion
        });
    }

    private async Task<bool> VerifyPacienteOwnership(string pacienteId, string userId, string role)
    {
        if (role == "paciente") return pacienteId == userId;
        if (role == "cuidador")
        {
            var cuidador = await _db.FindFirstOrDefaultAsync(_db.Cuidadores, c => c.UsuarioWebId == userId && c.PacienteId == pacienteId);
            return cuidador != null;
        }

        var pacientes = await _pacienteService.GetAllByUsuarioAsync(userId);
        return pacientes.Any(p => p.Id == pacienteId);
    }
}
