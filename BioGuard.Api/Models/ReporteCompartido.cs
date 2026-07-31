using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class ReporteCompartido
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("paciente_id")]
    public string PacienteId { get; set; } = string.Empty;

    [BsonElement("usuario_web_id")]
    public string UsuarioWebId { get; set; } = string.Empty;

    [BsonElement("token_acceso")]
    public string TokenAcceso { get; set; } = string.Empty;

    [BsonElement("tipo_reporte")]
    public string TipoReporte { get; set; } = string.Empty;

    [BsonElement("fecha_inicio")]
    public DateTime? FechaInicio { get; set; }

    [BsonElement("fecha_fin")]
    public DateTime? FechaFin { get; set; }

    [BsonElement("incluir_lecturas")]
    public bool IncluirLecturas { get; set; } = true;

    [BsonElement("incluir_eventos")]
    public bool IncluirEventos { get; set; } = true;

    [BsonElement("incluir_medicamentos")]
    public bool IncluirMedicamentos { get; set; } = false;

    [BsonElement("fecha_creacion")]
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    [BsonElement("fecha_expiracion")]
    public DateTime FechaExpiracion { get; set; } = DateTime.UtcNow.AddDays(7);

    [BsonElement("accesos")]
    public int Accesos { get; set; } = 0;

    [BsonElement("activo")]
    public bool Activo { get; set; } = true;
}
