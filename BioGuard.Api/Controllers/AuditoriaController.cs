using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Services;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 7: Auditoría (logs de actividad)
/// ENDPOINT WEB
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditoriaController : ControllerBase
{
    private readonly AuditoriaService _auditoriaService;
    private readonly ILogger<AuditoriaController> _logger;

    public AuditoriaController(AuditoriaService auditoriaService, ILogger<AuditoriaController> logger)
    {
        _auditoriaService = auditoriaService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/Auditoria [WEB]
    /// MÓDULO 7: Log de actividad de la cuenta (LOGIN, UPDATE, ALERTA, etc.)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int pagina = 1,
        [FromQuery] int porPagina = 50)
    {
        _logger.LogInformation("Listing audit logs, page {Pagina}, size {PorPagina}", pagina, porPagina);
        var registros = await _auditoriaService.ObtenerAsync(pagina, porPagina);
        var response = registros.Select(a => new
        {
            a.Id,
            a.Accion,
            a.TablaAfectada,
            a.RegistroId,
            a.Fecha,
            a.Ip
        });
        return Ok(response);
    }
}
