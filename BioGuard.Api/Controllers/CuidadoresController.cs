using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;
using BioGuard.Api.Config;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 4: Gestión de Cuidadores (varios por cuenta)
/// ENDPOINT WEB + MÓVIL
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CuidadoresController : ControllerBase
{
    private readonly CuidadorService _cuidadorService;
    private readonly PacienteService _pacienteService;
    private readonly IMongoDbContext _db;
    private readonly ILogger<CuidadoresController> _logger;

    public CuidadoresController(CuidadorService cuidadorService, PacienteService pacienteService, IMongoDbContext db, ILogger<CuidadoresController> logger)
    {
        _cuidadorService = cuidadorService;
        _pacienteService = pacienteService;
        _db = db;
        _logger = logger;
    }

    // ── Consulta ──────────────────────────────────────────────

    /// <summary>
    /// GET /api/Cuidadores [WEB]
    /// MÓDULO 4: Listar todos los cuidadores del usuario
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Listing cuidadores for user: {UserId}", usuarioId);
        var cuidadores = await _cuidadorService.ObtenerPorUsuarioAsync(usuarioId);
        var response = cuidadores.Select(c => new CuidadorResponse(
            c.Id, c.Nombre, c.Parentesco, c.PacienteId)).ToList();
        return Ok(response);
    }

    /// <summary>
    /// GET /api/Cuidadores/disponibles [WEB]
    /// MÓDULO 4: Cuántos puede agregar según plan (ej: "2/3")
    /// </summary>
    [HttpGet("disponibles")]
    public async Task<IActionResult> Disponibles()
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Checking available slots for user: {UserId}", usuarioId);
        var pacientes = await _pacienteService.GetAllByUsuarioAsync(usuarioId);
        var paciente = pacientes.FirstOrDefault();
        if (paciente == null)
        {
            _logger.LogWarning("No patient found for user: {UserId} when checking available slots", usuarioId);
            return Ok(new { Disponibles = 0, Total = 0 });
        }

        var count = await _cuidadorService.ContarPorPacienteAsync(paciente.Id);
        return Ok(new { Usados = count, Total = 3, Disponibles = 3 - count });
    }

    /// <summary>
    /// GET /api/Cuidadores/{id} [WEB + MÓVIL]
    /// MÓDULO 4: Detalle de un cuidador
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Fetching cuidador by ID: {CuidadorId}", id);
        var cuidador = await _cuidadorService.ObtenerPorIdAsync(id);
        if (cuidador == null)
        {
            _logger.LogWarning("Cuidador not found: {CuidadorId}", id);
            return NotFound();
        }
        if (cuidador.UsuarioWebId != usuarioId)
        {
            _logger.LogWarning("Ownership check failed fetching cuidador - user: {UserId}, cuidador: {CuidadorId}", usuarioId, id);
            return Forbid();
        }

        return Ok(new CuidadorResponse(
            cuidador.Id, cuidador.Nombre, cuidador.Parentesco,
            cuidador.PacienteId));
    }

    /// <summary>
    /// GET /api/Cuidadores/by-paciente/{pacienteId} [WEB + MÓVIL]
    /// MÓDULO 4: Cuidador(es) de un paciente específico
    /// </summary>
    [HttpGet("by-paciente/{pacienteId}")]
    public async Task<IActionResult> GetByPaciente(string pacienteId)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
        {
            _logger.LogWarning("Ownership check failed fetching cuidadores by paciente - user: {UserId}, paciente: {PacienteId}", usuarioId, pacienteId);
            return Forbid();
        }

        _logger.LogInformation("Fetching cuidadores for paciente: {PacienteId}", pacienteId);
        var cuidadores = await _cuidadorService.ObtenerPorPacienteAsync(pacienteId);
        var response = cuidadores.Select(c => new CuidadorResponse(
            c.Id, c.Nombre, c.Parentesco, c.PacienteId)).ToList();
        return Ok(response);
    }

    // ── Alta / Edición ────────────────────────────────────────

    /// <summary>
    /// POST /api/Cuidadores [WEB]
    /// MÓDULO 4: Crear cuidador + generar QR
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearCuidadorRequest request)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        var count = await _cuidadorService.ContarPorPacienteAsync(request.PacienteId);
        if (count >= 3)
        {
            _logger.LogWarning("Cuidador limit reached for paciente: {PacienteId}, user: {UserId}", request.PacienteId, usuarioId);
            return BadRequest(new { message = "Límite de cuidadores alcanzado" });
        }

        _logger.LogInformation("Creating cuidador for user: {UserId}, paciente: {PacienteId}", usuarioId, request.PacienteId);
        var (cuidador, codigo) = await _cuidadorService.CrearAsync(
            usuarioId, request.PacienteId, request.Nombre, request.Parentesco,
            request.Telefono, request.Correo);

        _logger.LogInformation("Cuidador created successfully for user: {UserId}", usuarioId);
        return Ok(new { CuidadorId = cuidador?.Id ?? "", CodigoAccesoQr = codigo, message = "Cuidador creado" });
    }

    /// <summary>
    /// PUT /api/Cuidadores/{id} [MÓVIL]
    /// MÓDULO 4: Editar nombre y parentesco
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Editar(string id, [FromBody] ActualizarCuidadorRequest request)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Editing cuidador: {CuidadorId}", id);
        var cuidador = await _cuidadorService.ObtenerPorIdAsync(id);
        if (cuidador == null)
        {
            _logger.LogWarning("Cuidador not found for edit: {CuidadorId}", id);
            return NotFound();
        }
        if (cuidador.UsuarioWebId != usuarioId)
        {
            _logger.LogWarning("Ownership check failed editing cuidador - user: {UserId}, cuidador: {CuidadorId}", usuarioId, id);
            return Forbid();
        }

        var result = await _cuidadorService.ActualizarAsync(id, request.Nombre, request.Parentesco);
        if (!result)
        {
            _logger.LogWarning("Cuidador update failed: {CuidadorId}", id);
            return NotFound();
        }
        _logger.LogInformation("Cuidador updated successfully: {CuidadorId}", id);
        return Ok(new { message = "Cuidador actualizado" });
    }

    /// <summary>
    /// DELETE /api/Cuidadores/{id} [WEB]
    /// MÓDULO 4: Revocar acceso del cuidador
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Eliminar(string id)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        _logger.LogInformation("Deleting cuidador: {CuidadorId}", id);
        var cuidador = await _cuidadorService.ObtenerPorIdAsync(id);
        if (cuidador == null)
        {
            _logger.LogWarning("Cuidador not found for deletion: {CuidadorId}", id);
            return NotFound();
        }
        if (cuidador.UsuarioWebId != usuarioId)
        {
            _logger.LogWarning("Ownership check failed deleting cuidador - user: {UserId}, cuidador: {CuidadorId}", usuarioId, id);
            return Forbid();
        }

        var result = await _cuidadorService.EliminarAsync(id);
        if (!result)
        {
            _logger.LogWarning("Cuidador deletion failed: {CuidadorId}", id);
            return NotFound();
        }
        _logger.LogInformation("Cuidador deleted successfully: {CuidadorId}", id);
        return NoContent();
    }

    // ── QR y Vinculación ──────────────────────────────────────

    /// <summary>
    /// GET /api/Cuidadores/{id}/qr [WEB]
    /// MÓDULO 4: Retornar QR y código para vinculación
    /// </summary>
    [HttpGet("{id}/qr")]
    public async Task<IActionResult> ObtenerQR(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();
        var cuidador = await _cuidadorService.ObtenerPorIdAsync(id);
        if (cuidador == null) return NotFound();
        if (cuidador.UsuarioWebId != userId && User.FindFirst(ClaimTypes.Role)?.Value != "admin") return Forbid();

        _logger.LogInformation("Fetching QR for cuidador: {CuidadorId}", id);
        return Ok(new { CodigoAccesoQr = cuidador.CodigoAccesoQr });
    }

    /// <summary>
    /// POST /api/Cuidadores/{id}/regenerar-qr [WEB]
    /// MÓDULO 4: Nuevo código (revoca el anterior)
    /// </summary>
    [HttpPost("{id}/regenerar-qr")]
    public async Task<IActionResult> RegenerarQR(string id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();
        var cuidador = await _cuidadorService.ObtenerPorIdAsync(id);
        if (cuidador == null) return NotFound();
        if (cuidador.UsuarioWebId != userId && User.FindFirst(ClaimTypes.Role)?.Value != "admin") return Forbid();

        _logger.LogInformation("Regenerating QR for cuidador: {CuidadorId}", id);

        var codigo = await _cuidadorService.RegenerarQRAsync(id);
        _logger.LogInformation("QR regenerated for cuidador: {CuidadorId}", id);
        return Ok(new { CodigoAccesoQr = codigo, message = "QR regenerado" });
    }

    private async Task<bool> VerifyPacienteOwnership(string pacienteId, string userId, string role)
    {
        if (role == "paciente") return pacienteId == userId;
        if (role == "cuidador")
        {
            var cuidador = await _db.FindFirstOrDefaultAsync(_db.Cuidadores, c => c.UsuarioWebId == userId && c.PacienteId == pacienteId);
            return cuidador != null;
        }

        var paciente = await _pacienteService.GetByIdAsync(pacienteId);
        return paciente?.UsuarioWebId == userId;
    }
}
