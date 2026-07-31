using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class EscalamientoPendiente
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("paciente_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string PacienteId { get; set; } = string.Empty;

    [BsonElement("alerta_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AlertaId { get; set; } = string.Empty;

    [BsonElement("fecha_ejecucion")]
    public DateTime FechaEjecucion { get; set; }

    [BsonElement("procesado")]
    public bool Procesado { get; set; } = false;
}
