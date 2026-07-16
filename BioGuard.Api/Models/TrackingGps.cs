using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class TrackingGps
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("meta")]
    public MetaData Meta { get; set; } = new();

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("ubicacion")]
    public UbicacionGps Ubicacion { get; set; } = new();

    [BsonElement("es_emergencia")]
    public bool EsEmergencia { get; set; } = false;
}
