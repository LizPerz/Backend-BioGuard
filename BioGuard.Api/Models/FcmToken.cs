using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class FcmToken
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("usuario_id")]
    public string UsuarioId { get; set; } = string.Empty;

    [BsonElement("rol")]
    public string Rol { get; set; } = string.Empty;

    [BsonElement("fcm_token")]
    public string Token { get; set; } = string.Empty;

    [BsonElement("plataforma")]
    public string Plataforma { get; set; } = "android";

    [BsonElement("activo")]
    public bool Activo { get; set; } = true;

    [BsonElement("fecha_registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
