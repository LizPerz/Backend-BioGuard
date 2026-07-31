using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class Dispositivo
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("paciente_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string PacienteId { get; set; } = string.Empty;

    [BsonElement("nombre_dispositivo")]
    public string NombreDispositivo { get; set; } = string.Empty;

    [BsonElement("mac_address")]
    public string MacAddress { get; set; } = string.Empty;

    [BsonElement("conectado")]
    public bool Conectado { get; set; }

    [BsonElement("fecha_vinculacion")]
    public DateTime FechaVinculacion { get; set; } = DateTime.UtcNow;

    [BsonElement("bateria")]
    public int? Bateria { get; set; }

    [BsonElement("ultima_sincronizacion")]
    public DateTime? UltimaSincronizacion { get; set; }

    [BsonElement("sensores_disponibles")]
    public List<string>? SensoresDisponibles { get; set; }
}
