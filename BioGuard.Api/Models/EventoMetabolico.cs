using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class EventoMetabolico
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("paciente_id")]
    public string PacienteId { get; set; } = string.Empty;

    [BsonElement("nivel_riesgo")]
    public string NivelRiesgo { get; set; } = string.Empty;

    [BsonElement("probabilidad_ml")]
    public double ProbabilidadMl { get; set; }

    [BsonElement("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [BsonElement("fecha_evento")]
    public DateTime FechaEvento { get; set; } = DateTime.UtcNow;

    [BsonElement("ubicacion_gps")]
    public UbicacionGps? UbicacionGps { get; set; }

    [BsonElement("atendida")]
    public bool Atendida { get; set; } = false;

    [BsonElement("acciones_tomadas")]
    public string? AccionesTomadas { get; set; }

    [BsonElement("atendido_por_id")]
    public string? AtendidoPorId { get; set; }

    [BsonElement("fecha_atencion")]
    public DateTime? FechaAtencion { get; set; }
}

public class UbicacionGps
{
    [BsonElement("type")]
    public string Type { get; set; } = "Point";

    [BsonElement("coordinates")]
    public double[] Coordinates { get; set; } = Array.Empty<double>();
}
