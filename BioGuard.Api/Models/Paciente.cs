using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class Paciente
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("usuario_web_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UsuarioWebId { get; set; } = string.Empty;

    [BsonElement("codigo_acceso_qr")]
    public string CodigoAccesoQr { get; set; } = string.Empty;

    [BsonElement("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [BsonElement("foto")]
    public string? Foto { get; set; }

    [BsonElement("fecha_nacimiento")]
    public DateTime? FechaNacimiento { get; set; }

    [BsonElement("biometria")]
    public Biometria Biometria { get; set; } = new();

    [BsonElement("dispositivo")]
    public DispositivoInfo Dispositivo { get; set; } = new();

    [BsonElement("perfil_completado")]
    public bool PerfilCompletado { get; set; } = false;

    [BsonElement("fecha_registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}

public class Biometria
{
    [BsonElement("edad")]
    public int Edad { get; set; }

    [BsonElement("peso_kg")]
    public double PesoKg { get; set; }

    [BsonElement("estatura_cm")]
    public double EstaturaCm { get; set; }

    [BsonElement("es_diabetico")]
    public bool EsDiabetico { get; set; }

    [BsonElement("familiares_diabetes")]
    public bool FamiliaresDiabetes { get; set; }

    [BsonElement("actividad_fisica")]
    public string ActividadFisica { get; set; } = string.Empty;
}

public class DispositivoInfo
{
    [BsonElement("mac_address")]
    public string MacAddress { get; set; } = string.Empty;

    [BsonElement("conectado")]
    public bool Conectado { get; set; }
}
