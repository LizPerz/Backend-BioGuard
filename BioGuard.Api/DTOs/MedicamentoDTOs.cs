using System.ComponentModel.DataAnnotations;

namespace BioGuard.Api.DTOs;

public record MedicamentoResponse(
    string Id, string PacienteId, string Nombre, string Dosis,
    string Horario, string? Notas, bool Activo,
    DateTime FechaCreacion, DateTime? UltimaToma);

public record CrearMedicamentoRequest(
    [Required] string PacienteId,
    [Required] [StringLength(200)] string Nombre,
    [Required] [StringLength(200)] string Dosis,
    [Required] [StringLength(200)] string Horario,
    [StringLength(500)] string? Notas);

public record ActualizarMedicamentoRequest(
    [Required] [StringLength(200)] string Nombre,
    [Required] [StringLength(200)] string Dosis,
    [Required] [StringLength(200)] string Horario,
    [StringLength(500)] string? Notas);

public record RegistrarTomaRequest(
    [Required] string MedicamentoId);
