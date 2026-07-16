using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class PrediccionMl
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("paciente_id")]
    public string PacienteId { get; set; } = string.Empty;

    [BsonElement("probabilidad_pico")]
    public double ProbabilidadPico { get; set; }

    [BsonElement("nivel_riesgo")]
    public string NivelRiesgo { get; set; } = string.Empty;

    [BsonElement("horas_estimadas")]
    public int? HorasEstimadas { get; set; }

    [BsonElement("recomendacion")]
    public string Recomendacion { get; set; } = string.Empty;

    [BsonElement("modelo_version")]
    public string ModeloVersion { get; set; } = string.Empty;

    [BsonElement("fecha_prediccion")]
    public DateTime FechaPrediccion { get; set; } = DateTime.UtcNow;

    [BsonElement("fecha_expiracion")]
    public DateTime FechaExpiracion { get; set; }
}
