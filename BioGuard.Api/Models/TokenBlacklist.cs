using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class TokenBlacklist
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("jti")]
    public string Jti { get; set; } = string.Empty;

    [BsonElement("revoked_at")]
    public DateTime RevokedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("expires_at")]
    public DateTime ExpiresAt { get; set; }
}
