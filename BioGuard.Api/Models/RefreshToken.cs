using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class RefreshToken
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("usuario_id")]
    public string UsuarioId { get; set; } = string.Empty;

    [BsonElement("token")]
    public string Token { get; set; } = string.Empty;

    [BsonElement("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    [BsonElement("replaced_by")]
    public string? ReplacedBy { get; set; }

    [BsonElement("ip")]
    public string? Ip { get; set; }

    [BsonIgnore]
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    [BsonIgnore]
    public bool IsRevoked => RevokedAt is not null;

    [BsonIgnore]
    public bool IsActive => !IsRevoked && !IsExpired;
}
