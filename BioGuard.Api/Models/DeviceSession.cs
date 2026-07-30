using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class DeviceSession
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("usuario_id")]
    public string UsuarioId { get; set; } = string.Empty;

    [BsonElement("rol")]
    public string Rol { get; set; } = string.Empty;

    [BsonElement("modelo_dispositivo")]
    public string? ModeloDispositivo { get; set; }

    [BsonElement("navegador")]
    public string? Navegador { get; set; }

    [BsonElement("sistema_operativo")]
    public string? SistemaOperativo { get; set; }

    [BsonElement("ip")]
    public string? Ip { get; set; }

    [BsonElement("user_agent")]
    public string? UserAgent { get; set; }

    [BsonElement("bateria")]
    public int? Bateria { get; set; }

    [BsonElement("ahorro_energia")]
    public bool AhorroEnergia { get; set; }

    [BsonElement("conectividad")]
    public string? Conectividad { get; set; }

    [BsonElement("ultimo_acceso")]
    public DateTime UltimoAcceso { get; set; } = DateTime.UtcNow;

    [BsonElement("activa")]
    public bool Activa { get; set; } = true;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("expire_at")]
    public DateTime ExpireAt { get; set; } = DateTime.UtcNow.AddDays(90);
}
