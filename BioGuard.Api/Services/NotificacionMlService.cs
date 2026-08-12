using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

/// <summary>
/// Servicio para enviar notificaciones push via webhooks/FCM
/// Se dispara cuando hay predicciones críticas de glucemia
/// </summary>
public interface INotificacionMlService
{
    Task NotificarPrediccionCriticaAsync(PrediccionMl prediccion);
    Task NotificarSincronizacionAsync(string pacienteId, int lote);
}

public class NotificacionMlService : INotificacionMlService
{
    private readonly ILogger<NotificacionMlService> _logger;
    private readonly IMongoDbContext _db;

    public NotificacionMlService(ILogger<NotificacionMlService> logger, IMongoDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    /// <summary>
    /// Enviar notificación push cuando hay predicción crítica
    /// Dispara webhooks a aplicaciones suscritas
    /// </summary>
    public async Task NotificarPrediccionCriticaAsync(PrediccionMl prediccion)
    {
        try
        {
            // Validar que sea crítica
            if (!EsCritica(prediccion))
            {
                _logger.LogInformation("Predicción {Id} no es crítica, no notificando", prediccion.Id);
                return;
            }

            _logger.LogInformation("Enviando notificación crítica para paciente {PacienteId}, P(Pico)={PPico}", 
                prediccion.PacienteId, prediccion.ProbabilidadPico);

            // Crear evento de notificación
            var evento = new NotificacionMlEvento
            {
                PacienteId = prediccion.PacienteId,
                PrediccionId = prediccion.Id,
                TipoEvento = "prediccion_critica",
                Descripcion = GenerarDescripcion(prediccion),
                NivelSeveridad = ObtenerSeveridad(prediccion),
                DatosAdicionales = new
                {
                    prediccion.ProbabilidadPico,
                    prediccion.NivelRiesgo,
                    prediccion.CasoClinico,
                    prediccion.Imc,
                    prediccion.Z,
                    prediccion.Recomendacion
                },
                FechaEvento = DateTime.UtcNow,
                Enviado = false
            };

            // Guardar evento
            await _db.NotificacionesMlEventos.InsertOneAsync(evento);

            // Disparar webhooks (simulado por ahora)
            await DispararWebhooksAsync(evento);

            // Marcar como enviado
            await _db.NotificacionesMlEventos.UpdateOneAsync(
                e => e.Id == evento.Id,
                new MongoDB.Driver.UpdateDefinitionBuilder<NotificacionMlEvento>()
                    .Set(e => e.Enviado, true)
                    .Set(e => e.FechaEnvio, DateTime.UtcNow)
            );

            _logger.LogInformation("Notificación critica enviada para predicción {Id}", prediccion.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notificando predicción crítica {Id}", prediccion.Id);
        }
    }

    /// <summary>
    /// Notificar sincronización completada
    /// </summary>
    public async Task NotificarSincronizacionAsync(string pacienteId, int lote)
    {
        try
        {
            _logger.LogInformation("Sincronización completada: {Lote} registros para paciente {PacienteId}", lote, pacienteId);

            var evento = new NotificacionMlEvento
            {
                PacienteId = pacienteId,
                TipoEvento = "sincronizacion_completada",
                Descripcion = $"Se sincronizaron {lote} reportes de predicción",
                NivelSeveridad = "info",
                FechaEvento = DateTime.UtcNow,
                Enviado = false
            };

            await _db.NotificacionesMlEventos.InsertOneAsync(evento);
            await DispararWebhooksAsync(evento);

            await _db.NotificacionesMlEventos.UpdateOneAsync(
                e => e.Id == evento.Id,
                new MongoDB.Driver.UpdateDefinitionBuilder<NotificacionMlEvento>()
                    .Set(e => e.Enviado, true)
                    .Set(e => e.FechaEnvio, DateTime.UtcNow)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notificando sincronización para paciente {PacienteId}", pacienteId);
        }
    }

    private bool EsCritica(PrediccionMl prediccion)
    {
        return prediccion.ProbabilidadPico >= 0.75 ||
               prediccion.NivelRiesgo?.Contains("Crítico", StringComparison.OrdinalIgnoreCase) == true ||
               prediccion.NivelRiesgo?.Contains("Alto", StringComparison.OrdinalIgnoreCase) == true;
    }

    private string GenerarDescripcion(PrediccionMl prediccion)
    {
        return prediccion.CasoClinico switch
        {
            "Hipoglucemia Nocturna" => 
                $"⚠️ ALERTA: {prediccion.CasoClinico} detectada. Probabilidad: {(prediccion.ProbabilidadPico * 100):F1}%",
            "Hiperglucemia Severa" => 
                $"⚠️ ALERTA: {prediccion.CasoClinico} detectada. Probabilidad: {(prediccion.ProbabilidadPico * 100):F1}%",
            _ => $"Predicción ML: {prediccion.CasoClinico} ({prediccion.NivelRiesgo})"
        };
    }

    private string ObtenerSeveridad(PrediccionMl prediccion)
    {
        return prediccion.NivelRiesgo?.ToLower() switch
        {
            "crítico alto" => "critical",
            "moderado alto" => "warning",
            "moderado" => "warning",
            _ => "info"
        };
    }

    private async Task DispararWebhooksAsync(NotificacionMlEvento evento)
    {
        try
        {
            // Aquí iría integración real con webhooks/FCM
            // Por ahora solo log
            _logger.LogInformation("Webhook disparado para evento {Id} tipo {Tipo}", evento.Id, evento.TipoEvento);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disparando webhook para evento {Id}", evento.Id);
        }
    }
}

/// <summary>
/// Modelo para eventos de notificación ML
/// Almacena historial de notificaciones enviadas
/// </summary>
public class NotificacionMlEvento
{
    public string Id { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
    public string PacienteId { get; set; }
    public string? PrediccionId { get; set; }
    public string TipoEvento { get; set; } // prediccion_critica, sincronizacion_completada, etc.
    public string Descripcion { get; set; }
    public string NivelSeveridad { get; set; } // critical, warning, info
    public object? DatosAdicionales { get; set; }
    public DateTime FechaEvento { get; set; }
    public DateTime? FechaEnvio { get; set; }
    public bool Enviado { get; set; }
    public int Reintentos { get; set; } = 0;
}
