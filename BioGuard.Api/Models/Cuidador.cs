using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class Cuidador
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("usuario_web_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UsuarioWebId { get; set; } = string.Empty;

    [BsonElement("paciente_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string PacienteId { get; set; } = string.Empty;

    [BsonElement("codigo_acceso_qr")]
    public string CodigoAccesoQr { get; set; } = string.Empty;

    [BsonElement("codigo_expira")]
    public DateTime? CodigoExpira { get; set; }

    [BsonElement("qr_usado")]
    public bool QrUsado { get; set; } = false;

    [BsonElement("usuario_vinculado_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? UsuarioVinculadoId { get; set; }

    [BsonElement("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [BsonElement("parentesco")]
    public string Parentesco { get; set; } = string.Empty;

    [BsonElement("telefono")]
    public string Telefono { get; set; } = string.Empty;

    [BsonElement("correo")]
    public string Correo { get; set; } = string.Empty;

    [BsonElement("fecha_autorizacion")]
    public DateTime FechaAutorizacion { get; set; } = DateTime.UtcNow;

    [BsonElement("foto")]
    public string? Foto { get; set; }
}
