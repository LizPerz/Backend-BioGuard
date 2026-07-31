using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public interface IRiesgoService
{
    Task<string> GetActiveModelVersionAsync();
    Task<ModeloMl?> GetActiveModelAsync();
}

public class RiesgoService : IRiesgoService
{
    private readonly IMongoDbContext _db;
    private readonly ILogger<RiesgoService> _logger;

    public RiesgoService(IMongoDbContext db, ILogger<RiesgoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string> GetActiveModelVersionAsync()
    {
        var modelo = await GetActiveModelAsync();
        return modelo?.Version ?? "rules-v1.0";
    }

    public async Task<ModeloMl?> GetActiveModelAsync()
    {
        return await _db.FindFirstOrDefaultAsync(_db.ModelosMl, m => m.Activo);
    }
}