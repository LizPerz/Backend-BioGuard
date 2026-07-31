using System.ComponentModel.DataAnnotations;

namespace BioGuard.Api.DTOs;

public record CrearEventoRequest(
    [Range(0.0, 1.0)] double Probabilidad,
    [Required] [StringLength(50)] string NivelRiesgo,
    [Required] [StringLength(500)] string Descripcion,
    Dictionary<string, double>? VariablesOrigen = null);