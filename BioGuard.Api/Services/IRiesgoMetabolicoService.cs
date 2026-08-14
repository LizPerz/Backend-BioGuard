using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public interface IRiesgoMetabolicoService
{
    Task<IrmeResult> CalculateAsync(string pacienteId, LecturaSensor lectura, bool isSleepTime = false);
    Task<PacienteBaseline> GetOrCreateBaselineAsync(string pacienteId);
    Task UpdateBaselineAsync(string pacienteId, LecturaSensor lectura);
    Task<AlertTrigger?> CheckAlertTriggerAsync(string pacienteId, IrmeResult irmeResult);
    string ClasificarPorMatriz(LecturaSensor lectura);
}

public record IrmeResult(
    int Score,
    string NivelRiesgo,
    IrmeComponents Components,
    string Recomendacion,
    int HorasEstimadas,
    string ModeloVersion
);

public record IrmeComponents(
    double FcRelativa,
    double HrvInversa,
    double TempRelativa,
    double ReposoPostEvento,
    double SuenoRiesgo,
    double HistorialPersonal,
    double ConfirmacionUsuario
);

public record PacienteBaseline(
    string PacienteId,
    double FcPromedioReposo,
    double HrvPromedio,
    double TempPromedio,
    int TotalLecturas,
    DateTime FechaCalculo,
    List<double> HistorialFc,
    List<double> HistorialHrv,
    List<double> HistorialTemp
);

public record AlertTrigger(
    string Tipo,
    string Nivel,
    string Titulo,
    string Mensaje,
    SensorData SensorData,
    bool EsCriticoNocturno
);