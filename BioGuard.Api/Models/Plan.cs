using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class Plan
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [BsonElement("precio")]
    public decimal Precio { get; set; }

    [BsonElement("precio_moneda")]
    public string PrecioMoneda { get; set; } = "USD";

    [BsonElement("limite_pacientes")]
    public int LimitePacientes { get; set; } = 1;

    [BsonElement("limite_cuidadores")]
    public int LimiteCuidadores { get; set; }

    [BsonElement("dias_historial")]
    public int DiasHistorial { get; set; }

    [BsonElement("gps_continuo")]
    public bool GpsContinuo { get; set; }

    [BsonElement("ai_console")]
    public bool AiConsole { get; set; }

    [BsonElement("stripe_price_id")]
    public string? StripePriceId { get; set; }

    [BsonElement("activo")]
    public bool Activo { get; set; } = true;

    [BsonElement("orden")]
    public int Orden { get; set; }

    [BsonElement("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
}
