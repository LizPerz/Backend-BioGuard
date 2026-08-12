using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Models;
using BioGuard.Api.Config;
using MongoDB.Driver;

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
                Mensaje = GenerarDescripcion(prediccion),
                NivelSeveridad = ObtenerSeveridad(prediccion),
                DatosContexto = new Dictionary<string, object>
                {
                    { "ProbabilidadPico", prediccion.ProbabilidadPico },
                    { "NivelRiesgo", prediccion.NivelRiesgo ?? "" },
                    { "CasoClinico", prediccion.CasoClinico ?? "" },
                    { "Imc", prediccion.Imc ?? 0 },
                    { "Z", prediccion.Z ?? 0 },
                    { "Recomendacion", prediccion.Recomendacion ?? "" }
                },
                FechaEvento = DateTime.UtcNow,
                EstadoEnvio = "PENDING"
            };

            // Guardar evento
            await _db.NotificacionesMlEventos.InsertOneAsync(evento);

            // Disparar webhooks (simulado por ahora)
            await DispararWebhooksAsync(evento);

            // Marcar como enviado
            var update = Builders<NotificacionMlEvento>.Update
                .Set(e => e.EstadoEnvio, "SENT");
            await _db.NotificacionesMlEventos.UpdateOneAsync(
                e => e.Id == evento.Id,
                update
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
                Mensaje = $"Se sincronizaron {lote} reportes de predicción",
                NivelSeveridad = "info",
                FechaEvento = DateTime.UtcNow,
                EstadoEnvio = "PENDING"
            };

            await _db.NotificacionesMlEventos.InsertOneAsync(evento);
            await DispararWebhooksAsync(evento);

            var update = Builders<NotificacionMlEvento>.Update
                .Set(e => e.EstadoEnvio, "SENT");
            await _db.NotificacionesMlEventos.UpdateOneAsync(
                e => e.Id == evento.Id,
                update
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
            "crítico alto" => "CRÍTICO",
            "moderado alto" => "ALTO",
            "moderado" => "MODERADO",
            _ => "INFO"
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
