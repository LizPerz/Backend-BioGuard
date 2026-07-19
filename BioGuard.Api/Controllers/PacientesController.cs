using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;

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

    public PacientesController(PacienteService pacienteService, SensorService sensorService,
        DispositivoService dispositivoService)
    {
        _pacienteService = pacienteService;
        _sensorService = sensorService;
        _dispositivoService = dispositivoService;
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

        var pacientes = await _pacienteService.GetAllByUsuarioAsync(usuarioId);
        var paciente = pacientes.FirstOrDefault();
        if (paciente == null) return NotFound(new { message = "No tiene paciente registrado" });

        return Ok(new PacienteResponse(
            paciente.Id, paciente.Nombre, paciente.Biometria?.EsDiabetico ?? false,
            paciente.PerfilCompletado, paciente.CodigoAccesoQr));
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
            return Forbid();

        var paciente = await _pacienteService.GetByIdAsync(id);
        if (paciente == null) return NotFound();

        return Ok(new PacienteResponse(
            paciente.Id, paciente.Nombre, paciente.Biometria?.EsDiabetico ?? false,
            paciente.PerfilCompletado, paciente.CodigoAccesoQr));
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
            return Forbid();

        var pacientes = await _pacienteService.GetAllByUsuarioAsync(usuarioWebId);
        var response = pacientes.Select(p => new PacienteResponse(
            p.Id, p.Nombre, p.Biometria?.EsDiabetico ?? false,
            p.PerfilCompletado, p.CodigoAccesoQr)).ToList();
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

        var codigo = await _pacienteService.CrearPacienteAsync(usuarioId, request.Nombre);
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
            return Forbid();

        var result = await _pacienteService.UpdateNombreAsync(id, request.Nombre);
        if (!result) return NotFound();
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
            return Forbid();

        var result = await _pacienteService.EliminarAsync(id);
        if (!result) return NotFound();
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
        await _pacienteService.UpdateBiometriaAsync(id, request);
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
        var paciente = await _pacienteService.GetByIdAsync(id);
        if (paciente == null) return NotFound();
        return Ok(new { CodigoAccesoQr = paciente.CodigoAccesoQr });
    }

    /// <summary>
    /// POST /api/Pacientes/{id}/regenerar-qr [WEB]
    /// MÓDULO 3: Generar nuevo código (revoca el anterior)
    /// </summary>
    [HttpPost("{id}/regenerar-qr")]
    public async Task<IActionResult> RegenerarQR(string id)
    {
        var paciente = await _pacienteService.GetByIdAsync(id);
        if (paciente == null) return NotFound();

        var codigo = await _pacienteService.CrearPacienteAsync(paciente.UsuarioWebId, paciente.Nombre);
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
            return Forbid();

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

        var pacientes = await _pacienteService.GetAllByUsuarioAsync(userId);
        return pacientes.Any(p => p.Id == pacienteId);
    }
}
