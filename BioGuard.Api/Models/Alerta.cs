using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class Alerta
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("paciente_id")]
    public string PacienteId { get; set; } = string.Empty;

    [BsonElement("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [BsonElement("nivel")]
    public string Nivel { get; set; } = string.Empty;

    [BsonElement("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [BsonElement("mensaje")]
    public string Mensaje { get; set; } = string.Empty;

    [BsonElement("sensor_data")]
    public SensorData? SensorData { get; set; }

    [BsonElement("atendida")]
    public bool Atendida { get; set; } = false;

    [BsonElement("atendida_por_id")]
    public string? AtendidaPorId { get; set; }

    [BsonElement("fecha_creacion")]
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    [BsonElement("fecha_atencion")]
    public DateTime? FechaAtencion { get; set; }
}

public class SensorData
{
    [BsonElement("pulso_bpm")]
    public int? PulsoBpm { get; set; }

    [BsonElement("temperatura_c")]
    public double? TemperaturaC { get; set; }

    [BsonElement("sudoracion_gsr")]
    public double? SudoracionGsr { get; set; }

    [BsonElement("probabilidad_pico")]
    public double? ProbabilidadPico { get; set; }
}
