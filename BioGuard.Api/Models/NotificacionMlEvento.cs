using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

/// <summary>
/// Registro de eventos de notificaciones ML disparadas
/// </summary>
public class NotificacionMlEvento
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("prediccion_id")]
    public string PrediccionId { get; set; } = string.Empty;

    [BsonElement("paciente_id")]
    public string PacienteId { get; set; } = string.Empty;

    [BsonElement("tipo_evento")]
    public string TipoEvento { get; set; } = string.Empty; // "CRITICAL_PEAK", "HIGH_RISK", etc.

    [BsonElement("nivel_severidad")]
    public string NivelSeveridad { get; set; } = string.Empty; // "CRÍTICO", "ALTO", "MODERADO"

    [BsonElement("mensaje")]
    public string Mensaje { get; set; } = string.Empty;

    [BsonElement("datos_contexto")]
    public Dictionary<string, object> DatosContexto { get; set; } = new();

    [BsonElement("webhook_url")]
    public string? WebhookUrl { get; set; }

    [BsonElement("estado_envio")]
    public string EstadoEnvio { get; set; } = "PENDING"; // PENDING, SENT, FAILED

    [BsonElement("intentos")]
    public int Intentos { get; set; } = 0;

    [BsonElement("error_mensaje")]
    public string? ErrorMensaje { get; set; }

    [BsonElement("fecha_evento")]
    public DateTime FechaEvento { get; set; } = DateTime.UtcNow;

    [BsonElement("fecha_proximo_reintento")]
    public DateTime? FechaProximoReintento { get; set; }
}
