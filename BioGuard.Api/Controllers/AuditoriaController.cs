using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Services;

namespace BioGuard.Api.Controllers;

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

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int pagina = 1,
        [FromQuery] int porPagina = 50)
    {
        try
        {
            if (pagina < 1) pagina = 1;
            if (porPagina < 1) porPagina = 50;
            if (porPagina > 200) porPagina = 200;

            _logger.LogInformation("Listing audit logs, page {Pagina}, size {PorPagina}", pagina, porPagina);

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            string? entidadId = null;

            if (role != "administrador")
            {
                entidadId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(entidadId))
                    return Unauthorized();
            }

            var registros = await _auditoriaService.ObtenerAsync(pagina, porPagina, entidadId);
            var response = registros.Select(a => new
            {
                id = a.Id,
                accion = a.Accion,
                tablaAfectada = a.TablaAfectada,
                registroId = a.RegistroId,
                fecha = a.Fecha,
                ip = a.Ip
            });
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing audit logs");
            return StatusCode(500, new { message = "Error al obtener registros de auditoría" });
        }
    }
}
