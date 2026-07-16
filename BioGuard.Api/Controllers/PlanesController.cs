using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.DTOs;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 2: Planes de Suscripción (catálogo)
/// ENDPOINT WEB
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PlanesController : ControllerBase
{
    private readonly MongoDbContext _db;

    public PlanesController(MongoDbContext db) => _db = db;

    // ── Consulta ──────────────────────────────────────────────
    // GET /api/Planes [WEB]

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var planes = await _db.Planes
            .Find(p => p.Activo)
            .SortBy(p => p.Orden)
            .ToListAsync();

        var response = planes.Select(p => new PlanResponse(
            p.Id, p.Nombre, p.Precio, p.PrecioMoneda,
            p.LimitePacientes, p.LimiteCuidadores, p.DiasHistorial,
            p.GpsContinuo, p.AiConsole, p.Descripcion
        )).ToList();

        return Ok(response);
    }

    // GET /api/Planes/{id} [WEB]

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var plan = await _db.Planes.Find(p => p.Id == id).FirstOrDefaultAsync();
        if (plan == null) return NotFound();

        return Ok(new PlanResponse(
            plan.Id, plan.Nombre, plan.Precio, plan.PrecioMoneda,
            plan.LimitePacientes, plan.LimiteCuidadores, plan.DiasHistorial,
            plan.GpsContinuo, plan.AiConsole, plan.Descripcion
        ));
    }
}
