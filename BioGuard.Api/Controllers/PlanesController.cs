using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
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

    // ── Alta / Edición ────────────────────────────────────────
    // POST /api/Planes [WEB] - Admin

    [HttpPost]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Crear([FromBody] CrearPlanRequest request)
    {
        var plan = new Plan
        {
            Nombre = request.Nombre,
            Precio = request.Precio,
            PrecioMoneda = request.PrecioMoneda,
            LimitePacientes = request.LimitePacientes,
            LimiteCuidadores = request.LimiteCuidadores,
            DiasHistorial = request.DiasHistorial,
            GpsContinuo = request.GpsContinuo,
            AiConsole = request.AiConsole,
            Descripcion = request.Descripcion,
            Activo = true,
            Orden = request.Orden
        };

        await _db.Planes.InsertOneAsync(plan);
        return Ok(new { PlanId = plan.Id, message = "Plan creado" });
    }

    // PUT /api/Planes/{id} [WEB] - Admin

    [HttpPut("{id}")]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Editar(string id, [FromBody] CrearPlanRequest request)
    {
        var update = Builders<Plan>.Update
            .Set(p => p.Nombre, request.Nombre)
            .Set(p => p.Precio, request.Precio)
            .Set(p => p.PrecioMoneda, request.PrecioMoneda)
            .Set(p => p.LimitePacientes, request.LimitePacientes)
            .Set(p => p.LimiteCuidadores, request.LimiteCuidadores)
            .Set(p => p.DiasHistorial, request.DiasHistorial)
            .Set(p => p.GpsContinuo, request.GpsContinuo)
            .Set(p => p.AiConsole, request.AiConsole)
            .Set(p => p.Descripcion, request.Descripcion)
            .Set(p => p.Orden, request.Orden);

        var result = await _db.Planes.UpdateOneAsync(p => p.Id == id, update);
        if (result.ModifiedCount == 0) return NotFound();
        return Ok(new { message = "Plan actualizado" });
    }

    // DELETE /api/Planes/{id} [WEB] - Admin

    [HttpDelete("{id}")]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Eliminar(string id)
    {
        var update = Builders<Plan>.Update.Set(p => p.Activo, false);
        var result = await _db.Planes.UpdateOneAsync(p => p.Id == id, update);
        if (result.ModifiedCount == 0) return NotFound();
        return Ok(new { message = "Plan desactivado" });
    }

    // POST /api/Planes/seed [WEB] - Admin

    [HttpPost("seed")]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> Seed()
    {
        var exists = await _db.FindToListAsync(_db.Planes, p => p.Activo == true);
        if (exists.Any()) return BadRequest(new { message = "Ya existen planes activos" });

        var planes = new List<Plan>
        {
            new()
            {
                Nombre = "Gratis", Precio = 0m, PrecioMoneda = "MXN",
                LimitePacientes = 1, LimiteCuidadores = 1, DiasHistorial = 7,
                GpsContinuo = false, AiConsole = false, Activo = true, Orden = 1,
                Descripcion = "Plan básico con funciones limitadas"
            },
            new()
            {
                Nombre = "Familiar", Precio = 5m, PrecioMoneda = "MXN",
                LimitePacientes = 1, LimiteCuidadores = 3, DiasHistorial = 30,
                GpsContinuo = true, AiConsole = false, Activo = true, Orden = 2,
                Descripcion = "Plan familiar con GPS y hasta 3 cuidadores"
            },
            new()
            {
                Nombre = "Pro", Precio = 10m, PrecioMoneda = "MXN",
                LimitePacientes = 1, LimiteCuidadores = 5, DiasHistorial = 90,
                GpsContinuo = true, AiConsole = true, Activo = true, Orden = 3,
                Descripcion = "Plan profesional con AI Console y funciones avanzadas"
            }
        };

        foreach (var plan in planes)
        {
            await _db.Planes.InsertOneAsync(plan);
        }

        return Ok(new { message = "Planes sembrados", total = planes.Count });
    }

    // POST /api/Planes/migrate-prices [WEB] - Admin
    // One-time endpoint to update existing plans to MXN pricing

    [HttpPost("migrate-prices")]
    [Authorize(Roles = "dueno")]
    public async Task<IActionResult> MigratePrices()
    {
        var precioMap = new Dictionary<string, (decimal Precio, string Desc)>
        {
            ["Gratis"] = (0m, "Plan básico con funciones limitadas"),
            ["Familiar"] = (5m, "Plan familiar con GPS y hasta 3 cuidadores"),
            ["Pro"] = (10m, "Plan profesional con AI Console y funciones avanzadas")
        };

        var updated = 0;
        foreach (var (nombre, (precio, desc)) in precioMap)
        {
            var update = Builders<Plan>.Update
                .Set(p => p.Precio, precio)
                .Set(p => p.PrecioMoneda, "MXN")
                .Set(p => p.Descripcion, desc);
            var result = await _db.Planes.UpdateOneAsync(p => p.Nombre == nombre, update);
            updated += (int)result.ModifiedCount;
        }

        return Ok(new { message = $"Planes actualizados a MXN", updated });
    }
}

public record CrearPlanRequest(
    [Required] string Nombre,
    [Required] decimal Precio,
    string PrecioMoneda = "MXN",
    int LimitePacientes = 1,
    int LimiteCuidadores = 0,
    int DiasHistorial = 30,
    bool GpsContinuo = false,
    bool AiConsole = false,
    [Required] string Descripcion = "",
    int Orden = 1);
