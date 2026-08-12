using Microsoft.Extensions.Logging;

namespace BioGuard.Api.Services;

public interface IRiesgoService
{
    Task<string> GetActiveModelVersionAsync();
}

public class RiesgoService : IRiesgoService
{
    private readonly ILogger<RiesgoService> _logger;

    public RiesgoService(ILogger<RiesgoService> logger)
    {
        _logger = logger;
    }

    public Task<string> GetActiveModelVersionAsync()
    {
        return Task.FromResult("pico-v1.0");
    }
}