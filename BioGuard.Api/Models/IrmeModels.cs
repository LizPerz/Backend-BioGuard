using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BioGuard.Api.Models;

public class PacienteDeviceInfo
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("paciente_id")]
    public string PacienteId { get; set; } = string.Empty;

    [BsonElement("modelo_dispositivo")]
    public string? ModeloDispositivo { get; set; }

    [BsonElement("fabricante")]
    public string? Fabricante { get; set; }

    [BsonElement("version_os")]
    public string? VersionOs { get; set; }

    [BsonElement("version_app")]
    public string? VersionApp { get; set; }

    [BsonElement("nivel_bateria")]
    public int? NivelBateria { get; set; }

    [BsonElement("ahorro_energia")]
    public bool? AhorroEnergia { get; set; }

    [BsonElement("conectividad")]
    public string? Conectividad { get; set; }

    [BsonElement("bluetooth_conectado")]
    public bool? BluetoothConectado { get; set; }

    [BsonElement("instalacion_id")]
    public string? InstalacionId { get; set; }

    [BsonElement("permisos_concedidos")]
    public List<string> PermisosConcedidos { get; set; } = new();

    [BsonElement("ultima_sincronizacion")]
    public DateTime? UltimaSincronizacion { get; set; }

    [BsonElement("fecha_registro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    [BsonElement("fecha_actualizacion")]
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}

public class WebSessionInfo
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("usuario_web_id")]
    public string UsuarioWebId { get; set; } = string.Empty;

    [BsonElement("navegador")]
    public string? Navegador { get; set; }

    [BsonElement("version_navegador")]
    public string? VersionNavegador { get; set; }

    [BsonElement("sistema_operativo")]
    public string? SistemaOperativo { get; set; }

    [BsonElement("ip_aproximada")]
    public string? IpAproximada { get; set; }

    [BsonElement("pais_ip")]
    public string? PaisIp { get; set; }

    [BsonElement("ultimo_acceso")]
    public DateTime UltimoAcceso { get; set; } = DateTime.UtcNow;

    [BsonElement("activa")]
    public bool Activa { get; set; } = true;

    [BsonElement("user_agent")]
    public string? UserAgent { get; set; }
}

public class CuidadorNivelAcceso
{
    public const string SoloAlertas = "solo_alertas";
    public const string ResumenSemanal = "resumen_semanal";
    public const string HistorialCompleto = "historial_completo";
}