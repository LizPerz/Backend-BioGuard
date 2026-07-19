using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;

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

    public CuidadoresController(CuidadorService cuidadorService, PacienteService pacienteService)
    {
        _cuidadorService = cuidadorService;
        _pacienteService = pacienteService;
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

        var cuidadores = await _cuidadorService.ObtenerPorUsuarioAsync(usuarioId);
        var response = cuidadores.Select(c => new CuidadorResponse(
            c.Id, c.Nombre, c.Parentesco, c.PacienteId, c.CodigoAccesoQr)).ToList();
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

        var pacientes = await _pacienteService.GetAllByUsuarioAsync(usuarioId);
        var paciente = pacientes.FirstOrDefault();
        if (paciente == null) return Ok(new { Disponibles = 0, Total = 0 });

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

        var cuidador = await _cuidadorService.ObtenerPorIdAsync(id);
        if (cuidador == null) return NotFound();
        if (cuidador.UsuarioWebId != usuarioId) return Forbid();

        return Ok(new CuidadorResponse(
            cuidador.Id, cuidador.Nombre, cuidador.Parentesco,
            cuidador.PacienteId, cuidador.CodigoAccesoQr));
    }

    /// <summary>
    /// GET /api/Cuidadores/by-paciente/{pacienteId} [WEB + MÓVIL]
    /// MÓDULO 4: Cuidador(es) de un paciente específico
    /// </summary>
    [HttpGet("by-paciente/{pacienteId}")]
    public async Task<IActionResult> GetByPaciente(string pacienteId)
    {
        var cuidadores = await _cuidadorService.ObtenerPorPacienteAsync(pacienteId);
        var response = cuidadores.Select(c => new CuidadorResponse(
            c.Id, c.Nombre, c.Parentesco, c.PacienteId, c.CodigoAccesoQr)).ToList();
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
        if (count >= 3) return BadRequest(new { message = "Límite de cuidadores alcanzado" });

        var (cuidador, codigo) = await _cuidadorService.CrearAsync(
            usuarioId, request.PacienteId, request.Nombre, request.Parentesco,
            request.Telefono, request.Correo);

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

        var cuidador = await _cuidadorService.ObtenerPorIdAsync(id);
        if (cuidador == null) return NotFound();
        if (cuidador.UsuarioWebId != usuarioId) return Forbid();

        var result = await _cuidadorService.ActualizarAsync(id, request.Nombre, request.Parentesco);
        if (!result) return NotFound();
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

        var cuidador = await _cuidadorService.ObtenerPorIdAsync(id);
        if (cuidador == null) return NotFound();
        if (cuidador.UsuarioWebId != usuarioId) return Forbid();

        var result = await _cuidadorService.EliminarAsync(id);
        if (!result) return NotFound();
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
        var cuidador = await _cuidadorService.ObtenerPorIdAsync(id);
        if (cuidador == null) return NotFound();
        return Ok(new { CodigoAccesoQr = cuidador.CodigoAccesoQr });
    }

    /// <summary>
    /// POST /api/Cuidadores/{id}/regenerar-qr [WEB]
    /// MÓDULO 4: Nuevo código (revoca el anterior)
    /// </summary>
    [HttpPost("{id}/regenerar-qr")]
    public async Task<IActionResult> RegenerarQR(string id)
    {
        var cuidador = await _cuidadorService.ObtenerPorIdAsync(id);
        if (cuidador == null) return NotFound();

        var codigo = await _cuidadorService.RegenerarQRAsync(id);
        return Ok(new { CodigoAccesoQr = codigo, message = "QR regenerado" });
    }
}
