using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using BioGuard.Api.Services;

namespace BioGuard.Api.Services;

public class EscalamientoBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EscalamientoBackgroundService> _logger;

    public EscalamientoBackgroundService(IServiceProvider serviceProvider, ILogger<EscalamientoBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Servicio de Escalamiento en Segundo Plano iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IMongoDbContext>();
                var sensorService = scope.ServiceProvider.GetRequiredService<SensorService>();

                var ahora = DateTime.UtcNow;
                var pendientes = await db.FindToListAsync(db.EscalamientosPendientes, 
                    e => !e.Procesado && e.FechaEjecucion <= ahora);

                foreach (var pend in pendientes)
                {
                    try
                    {
                        _logger.LogInformation("Ejecutando escalamiento nocturno pendiente para paciente {PacienteId}", pend.PacienteId);
                        
                        await sensorService.EjecutarProtocoloEscalamientoAsync(pend.PacienteId, pend.AlertaId);

                        var update = Builders<EscalamientoPendiente>.Update.Set(e => e.Procesado, true);
                        await db.EscalamientosPendientes.UpdateOneAsync(e => e.Id == pend.Id, update);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error ejecutando escalamiento para paciente {PacienteId}", pend.PacienteId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el ciclo del servicio de escalamiento");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
