using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 5: Medicamentos (trigger-based por ML)
/// ENDPOINT WEB + MÓVIL
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MedicamentosController : ControllerBase
{
    private readonly MedicamentoService _medicamentoService;
    private readonly PacienteService _pacienteService;

    public MedicamentosController(MedicamentoService medicamentoService, PacienteService pacienteService)
    {
        _medicamentoService = medicamentoService;
        _pacienteService = pacienteService;
    }

    /// <summary>
    /// GET /api/Medicamentos/by-paciente/{pacienteId} [WEB + MÓVIL]
    /// </summary>
    [HttpGet("by-paciente/{pacienteId}")]
    public async Task<IActionResult> ObtenerPorPaciente(string pacienteId)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(pacienteId, usuarioId, role!))
            return Forbid();

        var medicamentos = await _medicamentoService.ObtenerPorPacienteAsync(pacienteId);
        var response = medicamentos.Select(m => new MedicamentoResponse(
            m.Id, m.PacienteId, m.Nombre, m.Dosis, m.Horario,
            m.Notas, m.Activo, m.FechaCreacion, m.UltimaToma));
        return Ok(response);
    }

    /// <summary>
    /// GET /api/Medicamentos/{id} [WEB + MÓVIL]
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var medicamento = await _medicamentoService.ObtenerPorIdAsync(id);
        if (medicamento == null) return NotFound();

        var usuarioId = User.FindFirst("sub")?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        if (!await VerifyPacienteOwnership(medicamento.PacienteId, usuarioId, role!))
            return Forbid();

        return Ok(new MedicamentoResponse(
            medicamento.Id, medicamento.PacienteId, medicamento.Nombre,
            medicamento.Dosis, medicamento.Horario, medicamento.Notas,
            medicamento.Activo, medicamento.FechaCreacion, medicamento.UltimaToma));
    }

    /// <summary>
    /// POST /api/Medicamentos [WEB]
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Crear([FromBody] CrearMedicamentoRequest request)
    {
        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        var paciente = await _pacienteService.GetByIdAsync(request.PacienteId);
        if (paciente == null) return NotFound(new { message = "Paciente no encontrado" });
        if (paciente.UsuarioWebId != usuarioId) return Forbid();

        var medicamento = await _medicamentoService.CrearAsync(
            request.PacienteId, request.Nombre, request.Dosis,
            request.Horario, request.Notas);

        return Ok(new { MedicamentoId = medicamento.Id, message = "Medicamento creado" });
    }

    /// <summary>
    /// POST /api/Medicamentos/trigger [MÓVIL/ML]
    /// ML crea "toma tu medicamento" al detectar pico crítico
    /// </summary>
    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerMedicamento([FromBody] CrearMedicamentoRequest request)
    {
        var medicamento = await _medicamentoService.CrearAsync(
            request.PacienteId, request.Nombre, request.Dosis,
            request.Horario, request.Notas);

        return Ok(new { MedicamentoId = medicamento.Id, message = "Medicamento registrado por ML" });
    }

    /// <summary>
    /// PUT /api/Medicamentos/{id} [WEB]
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Editar(string id, [FromBody] ActualizarMedicamentoRequest request)
    {
        var medicamento = await _medicamentoService.ObtenerPorIdAsync(id);
        if (medicamento == null) return NotFound();

        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        var paciente = await _pacienteService.GetByIdAsync(medicamento.PacienteId);
        if (paciente?.UsuarioWebId != usuarioId) return Forbid();

        var result = await _medicamentoService.ActualizarAsync(
            id, request.Nombre, request.Dosis, request.Horario, request.Notas);
        if (!result) return NotFound();
        return Ok(new { message = "Medicamento actualizado" });
    }

    /// <summary>
    /// PUT /api/Medicamentos/{id}/toma [MÓVIL]
    /// Registrar que el paciente tomó el medicamento
    /// </summary>
    [HttpPut("{id}/toma")]
    public async Task<IActionResult> RegistrarToma(string id)
    {
        var result = await _medicamentoService.RegistrarTomaAsync(id);
        if (!result) return NotFound();
        return Ok(new { message = "Toma registrada" });
    }

    /// <summary>
    /// PUT /api/Medicamentos/{id}/activo [WEB]
    /// </summary>
    [HttpPut("{id}/activo")]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> CambiarActivo(string id, [FromBody] bool activo)
    {
        var result = await _medicamentoService.ActivarAsync(id, activo);
        if (!result) return NotFound();
        return Ok(new { message = activo ? "Medicamento activado" : "Medicamento desactivado" });
    }

    /// <summary>
    /// DELETE /api/Medicamentos/{id} [WEB]
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Eliminar(string id)
    {
        var medicamento = await _medicamentoService.ObtenerPorIdAsync(id);
        if (medicamento == null) return NotFound();

        var usuarioId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(usuarioId)) return Unauthorized();

        var paciente = await _pacienteService.GetByIdAsync(medicamento.PacienteId);
        if (paciente?.UsuarioWebId != usuarioId) return Forbid();

        var result = await _medicamentoService.EliminarAsync(id);
        if (!result) return NotFound();
        return NoContent();
    }

    private async Task<bool> VerifyPacienteOwnership(string pacienteId, string userId, string role)
    {
        if (role == "paciente") return pacienteId == userId;
        if (role == "cuidador") return true;

        var paciente = await _pacienteService.GetByIdAsync(pacienteId);
        return paciente?.UsuarioWebId == userId;
    }
}
