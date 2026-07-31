using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class Notificacion
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("paciente_id")]
    public string PacienteId { get; set; } = string.Empty;

    [BsonElement("cuidador_id")]
    public string? CuidadorId { get; set; }

    [BsonElement("usuario_web_id")]
    public string? UsuarioWebId { get; set; }

    [BsonElement("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [BsonElement("mensaje")]
    public string Mensaje { get; set; } = string.Empty;

    [BsonElement("tipo")]
    public string Tipo { get; set; } = "sistema";

    [BsonElement("leida")]
    public bool Leida { get; set; } = false;

    [BsonElement("enviada")]
    public bool Enviada { get; set; } = false;

    [BsonElement("fecha_envio")]
    public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;
}
