using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class Auditoria
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("entidad_id")]
    public string EntidadId { get; set; } = string.Empty;

    [BsonElement("accion")]
    public string Accion { get; set; } = string.Empty;

    [BsonElement("tabla_afectada")]
    public string TablaAfectada { get; set; } = string.Empty;

    [BsonElement("registro_id")]
    public string RegistroId { get; set; } = string.Empty;

    [BsonElement("fecha")]
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [BsonElement("ip")]
    public string Ip { get; set; } = string.Empty;
}
