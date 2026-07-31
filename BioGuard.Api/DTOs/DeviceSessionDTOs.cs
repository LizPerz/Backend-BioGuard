using System.ComponentModel.DataAnnotations;

namespace BioGuard.Api.DTOs;

public record RegistrarSesionTelefonoRequest(
    [Required] string ModeloDispositivo,
    [Required] string SistemaOperativo,
    int? Bateria,
    bool AhorroEnergia,
    string? Conectividad);
