namespace BioGuard.Api.DTOs;

public record ReporteResumenResponse(
    int TotalLecturas,
    int TotalEventos,
    int TotalAlertas,
    int TotalMedicamentos,
    int EventosCriticos,
    int AlertasPendientes,
    double PromedioPulso,
    DateTime? UltimaLectura);

public record ReporteAlertaResponse(
    string Id, string Tipo, string Nivel, string Titulo,
    string Mensaje, bool Atendida,
    DateTime FechaCreacion, DateTime? FechaAtencion);

public record ReporteEventoResponse(
    string Id, string NivelRiesgo, double ProbabilidadMl,
    string Descripcion, DateTime FechaEvento, bool Atendida);

public record ReporteMedicamentoResponse(
    string Id, string Nombre, string Dosis, string Horario,
    bool Activo, DateTime? UltimaToma);
