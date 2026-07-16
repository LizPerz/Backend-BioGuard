using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;

namespace BioGuard.Api.Controllers;

/// <summary>
/// MÓDULO 2: Planes de Suscripción (catálogo)
/// ENDPOINT WEB
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PlanesController : ControllerBase
{
    private readonly IMongoDbContext _db;

    public PlanesController(IMongoDbContext db) => _db = db;

    // ── Consulta ──────────────────────────────────────────────
    // GET /api/Planes [WEB]

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var filter = Builders<Plan>.Filter.Eq(p => p.Activo, true);
        var sort = Builders<Plan>.Sort.Ascending(p => p.Orden);
        var planes = await _db.FindToListAsync(_db.Planes, filter, sort);

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
        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Id == id);
        if (plan == null) return NotFound();

        return Ok(new PlanResponse(
            plan.Id, plan.Nombre, plan.Precio, plan.PrecioMoneda,
            plan.LimitePacientes, plan.LimiteCuidadores, plan.DiasHistorial,
            plan.GpsContinuo, plan.AiConsole, plan.Descripcion
        ));
    }
}
