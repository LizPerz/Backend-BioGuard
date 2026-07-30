using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public interface IPlanLimiteService
{
    Task<PlanLimiteResult> VerificarLimiteCuidadoresAsync(string duenoId, string pacienteId);
    Task<PlanLimiteResult> VerificarDiasHistorialAsync(string duenoId, int diasSolicitados);
    Task<PlanLimiteResult> VerificarGpsContinuoAsync(string duenoId);
    Task<PlanLimiteResult> VerificarAiConsoleAsync(string duenoId);
    Task<PlanLimiteResult> VerificarExportacionReportesAsync(string duenoId);
    Task<PlanLimiteResult> VerificarGuardianNocturnoAsync(string duenoId);
}

public record PlanLimiteResult(bool Permitido, string? Motivo = null);

public class PlanLimiteService : IPlanLimiteService
{
    private readonly IMongoDbContext _db;
    private readonly ILogger<PlanLimiteService> _logger;

    public PlanLimiteService(IMongoDbContext db, ILogger<PlanLimiteService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private async Task<Plan?> GetPlanByDuenoIdAsync(string duenoId)
    {
        var dueno = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Id == duenoId);
        if (dueno == null) return null;
        return await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Id == dueno.PlanId);
    }

    public async Task<PlanLimiteResult> VerificarLimiteCuidadoresAsync(string duenoId, string pacienteId)
    {
        var plan = await GetPlanByDuenoIdAsync(duenoId);
        if (plan == null) return new PlanLimiteResult(false, "Plan no encontrado");

        var count = await _db.CountDocumentsAsync(_db.Cuidadores, c => c.PacienteId == pacienteId);
        if (count >= plan.LimiteCuidadores)
            return new PlanLimiteResult(false, $"Límite de cuidadores alcanzado ({plan.LimiteCuidadores})");

        return new PlanLimiteResult(true);
    }

    public async Task<PlanLimiteResult> VerificarDiasHistorialAsync(string duenoId, int diasSolicitados)
    {
        var plan = await GetPlanByDuenoIdAsync(duenoId);
        if (plan == null) return new PlanLimiteResult(false, "Plan no encontrado");

        if (diasSolicitados > plan.DiasHistorial)
            return new PlanLimiteResult(false, $"Tu plan permite hasta {plan.DiasHistorial} días de historial");

        return new PlanLimiteResult(true);
    }

    public async Task<PlanLimiteResult> VerificarGpsContinuoAsync(string duenoId)
    {
        var plan = await GetPlanByDuenoIdAsync(duenoId);
        if (plan == null) return new PlanLimiteResult(false, "Plan no encontrado");

        if (!plan.GpsContinuo)
            return new PlanLimiteResult(false, "GPS continuo no disponible en tu plan");

        return new PlanLimiteResult(true);
    }

    public async Task<PlanLimiteResult> VerificarAiConsoleAsync(string duenoId)
    {
        var plan = await GetPlanByDuenoIdAsync(duenoId);
        if (plan == null) return new PlanLimiteResult(false, "Plan no encontrado");

        if (!plan.AiConsole)
            return new PlanLimiteResult(false, "Consola de IA no disponible en tu plan");

        return new PlanLimiteResult(true);
    }

    public async Task<PlanLimiteResult> VerificarExportacionReportesAsync(string duenoId)
    {
        var plan = await GetPlanByDuenoIdAsync(duenoId);
        if (plan == null) return new PlanLimiteResult(false, "Plan no encontrado");

        if (plan.DiasHistorial < 90)
            return new PlanLimiteResult(false, "Exportación de reportes no disponible en tu plan");

        return new PlanLimiteResult(true);
    }

    public async Task<PlanLimiteResult> VerificarGuardianNocturnoAsync(string duenoId)
    {
        var plan = await GetPlanByDuenoIdAsync(duenoId);
        if (plan == null) return new PlanLimiteResult(false, "Plan no encontrado");

        if (plan.Precio <= 0)
            return new PlanLimiteResult(false, "Guardián Nocturno no disponible en plan Gratis");

        return new PlanLimiteResult(true);
    }
}
