using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class TicketSoporte
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("usuario_id")]
    public string UsuarioId { get; set; } = string.Empty;

    [BsonElement("asunto")]
    public string Asunto { get; set; } = string.Empty;

    [BsonElement("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [BsonElement("categoria")]
    public string Categoria { get; set; } = string.Empty;

    [BsonElement("prioridad")]
    public string Prioridad { get; set; } = "normal";

    [BsonElement("estado")]
    public string Estado { get; set; } = "abierto";

    [BsonElement("mensajes")]
    public List<MensajeSoporte> Mensajes { get; set; } = new();

    [BsonElement("fecha_creacion")]
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    [BsonElement("fecha_actualizacion")]
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    [BsonElement("fecha_cierre")]
    public DateTime? FechaCierre { get; set; }

    [BsonElement("asignado_a")]
    public string? AsignadoA { get; set; }

    [BsonElement("cerrado_por")]
    public string? CerradoPor { get; set; }
}

public class MensajeSoporte
{
    [BsonElement("autor_id")]
    public string AutorId { get; set; } = string.Empty;

    [BsonElement("autor_nombre")]
    public string AutorNombre { get; set; } = string.Empty;

    [BsonElement("contenido")]
    public string Contenido { get; set; } = string.Empty;

    [BsonElement("fecha")]
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [BsonElement("es_admin")]
    public bool EsAdmin { get; set; } = false;
}
