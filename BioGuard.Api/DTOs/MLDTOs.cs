using System.ComponentModel.DataAnnotations;

namespace BioGuard.Api.DTOs;

public record EntrenarModeloRequest(
    [Required] [StringLength(50)] string Version,
    [StringLength(500)] string Descripcion);

public record DiagnosticarRequest(
    [Required] [StringLength(100)] string PacienteId);