using System.ComponentModel.DataAnnotations;

namespace BioGuard.Api.DTOs;

/// <summary>
/// Request DTO para guardar predicción ML
/// El móvil calcula localmente F1-F3 y envía el reporte
/// </summary>
public class GuardarPrediccionRequest
{
    [Required]
    [StringLength(255)]
    public string PacienteId { get; set; }

    [Range(0.0, 1.0)]
    public double ProbabilidadPico { get; set; }

    [StringLength(50)]
    public string NivelRiesgo { get; set; }

    [StringLength(255)]
    public string? CasoClinico { get; set; }

    [StringLength(500)]
    public string? Recomendacion { get; set; }

    [StringLength(500)]
    public string? AccionAutomatizada { get; set; }

    public double? Imc { get; set; }

    public double? Z { get; set; }

    public double? PPico { get; set; }

    public int? HorasEstimadas { get; set; }

    [StringLength(50)]
    public string? ModeloVersion { get; set; }
}

/// <summary>
/// Response DTO para predicción ML
/// Usado en GET endpoints
/// </summary>
public class PrediccionMlResponse
{
    public string Id { get; set; }
    public string PacienteId { get; set; }
    public double ProbabilidadPico { get; set; }
    public string NivelRiesgo { get; set; }
    public string? CasoClinico { get; set; }
    public double? Imc { get; set; }
    public double? Z { get; set; }
    public double? PPico { get; set; }
    public string? Recomendacion { get; set; }
    public string? AccionAutomatizada { get; set; }
    public string? ModeloVersion { get; set; }
    public DateTime FechaPrediccion { get; set; }
}
