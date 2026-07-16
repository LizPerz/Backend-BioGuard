using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class Pago
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("usuario_web_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UsuarioWebId { get; set; } = string.Empty;

    [BsonElement("monto")]
    public decimal Monto { get; set; }

    [BsonElement("moneda")]
    public string Moneda { get; set; } = "USD";

    [BsonElement("plan_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string PlanId { get; set; } = string.Empty;

    [BsonElement("stripe_session_id")]
    public string? StripeSessionId { get; set; }

    [BsonElement("stripe_customer_id")]
    public string? StripeCustomerId { get; set; }

    [BsonElement("estado")]
    public string Estado { get; set; } = "pendiente";

    [BsonElement("fecha_pago")]
    public DateTime FechaPago { get; set; } = DateTime.UtcNow;

    [BsonElement("metodo_pago")]
    public string MetodoPago { get; set; } = "tarjeta";

    [BsonElement("recibo_url")]
    public string? ReciboUrl { get; set; }
}
