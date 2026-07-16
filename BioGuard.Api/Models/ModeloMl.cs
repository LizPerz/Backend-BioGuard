using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class ModeloMl
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("version")]
    public string Version { get; set; } = string.Empty;

    [BsonElement("fecha_entrenamiento")]
    public DateTime FechaEntrenamiento { get; set; } = DateTime.UtcNow;

    [BsonElement("accuracy")]
    public double Accuracy { get; set; }

    [BsonElement("precision")]
    public double Precision { get; set; }

    [BsonElement("recall")]
    public double Recall { get; set; }

    [BsonElement("f1_score")]
    public double F1Score { get; set; }

    [BsonElement("total_muestras")]
    public int TotalMuestras { get; set; }

    [BsonElement("activo")]
    public bool Activo { get; set; } = false;

    [BsonElement("descripcion")]
    public string Descripcion { get; set; } = string.Empty;
}
