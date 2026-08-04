using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BioGuard.Api.Services;

public class MLPredictionClient
{
    private readonly HttpClient _httpClient;
    private readonly MLOptions _options;
    private readonly ILogger<MLPredictionClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public MLPredictionClient(HttpClient httpClient, IOptions<MLOptions> options, ILogger<MLPredictionClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_httpClient.BaseAddress?.ToString());

    public async Task<MLPredictionResponseDto?> PredecirAsync(string pacienteId, List<LecturaSensor> lecturas, CancellationToken ct = default)
    {
        if (lecturas.Count == 0)
            throw new InvalidOperationException("No hay lecturas del paciente para predecir.");

        if (!IsConfigured)
            throw new InvalidOperationException("El microservicio ML no está configurado (ML:BaseUrl / ML_API_URL).");

        var request = new MLPredictRequestDto
        {
            PacienteId = pacienteId,
            Lecturas = lecturas
                .OrderByDescending(l => l.Timestamp)
                .Take(100)
                .OrderBy(l => l.Timestamp)
                .Select(l => new MLLecturaDto
                {
                    PacienteId = pacienteId,
                    Timestamp = l.Timestamp,
                    PulsoBpm = l.PulsoBpm,
                    TemperaturaC = l.TemperaturaC,
                    SudoracionGsr = l.SudoracionGsr,
                    ProbabilidadPico = l.ProbabilidadPico
                })
                .ToList()
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("api/v1/predicciones", request, JsonOptions, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("ML service responded {Status} for patient {PacienteId}: {Body}",
                    (int)response.StatusCode, pacienteId, body);
                return null;
            }

            var prediccion = await response.Content.ReadFromJsonAsync<MLPredictionResponseDto>(JsonOptions, ct);
            if (prediccion != null)
            {
                prediccion.PacienteId = pacienteId;
                prediccion.FechaPrediccion = NormalizeUtc(prediccion.FechaPrediccion);
                prediccion.FechaExpiracion = NormalizeUtc(prediccion.FechaExpiracion);
            }
            return prediccion;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("ML service timed out for patient {PacienteId}", pacienteId);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ML service unreachable for patient {PacienteId}", pacienteId);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid response from ML service for patient {PacienteId}", pacienteId);
            return null;
        }
    }

    private static DateTime NormalizeUtc(DateTime fecha)
    {
        if (fecha == default)
            return default;
        return fecha.Kind switch
        {
            DateTimeKind.Utc => fecha,
            DateTimeKind.Local => fecha.ToUniversalTime(),
            _ => DateTime.SpecifyKind(fecha, DateTimeKind.Utc)
        };
    }
}

public class MLPredictRequestDto
{
    [JsonPropertyName("paciente_id")]
    public string PacienteId { get; set; } = string.Empty;

    [JsonPropertyName("lecturas")]
    public List<MLLecturaDto> Lecturas { get; set; } = new();
}

public class MLLecturaDto
{
    [JsonPropertyName("paciente_id")]
    public string PacienteId { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("pulso_bpm")]
    public int PulsoBpm { get; set; }

    [JsonPropertyName("temperatura_c")]
    public double TemperaturaC { get; set; }

    [JsonPropertyName("sudoracion_gsr")]
    public double SudoracionGsr { get; set; }

    [JsonPropertyName("probabilidad_pico")]
    public double ProbabilidadPico { get; set; }
}

public class MLPredictionResponseDto
{
    [JsonPropertyName("paciente_id")]
    public string PacienteId { get; set; } = string.Empty;

    [JsonPropertyName("probabilidad_pico")]
    public double ProbabilidadPico { get; set; }

    [JsonPropertyName("nivel_riesgo")]
    public string NivelRiesgo { get; set; } = string.Empty;

    [JsonPropertyName("horas_estimadas")]
    public int? HorasEstimadas { get; set; }

    [JsonPropertyName("recomendacion")]
    public string Recomendacion { get; set; } = string.Empty;

    [JsonPropertyName("modelo_version")]
    public string ModeloVersion { get; set; } = string.Empty;

    [JsonPropertyName("fecha_prediccion")]
    public DateTime FechaPrediccion { get; set; }

    [JsonPropertyName("fecha_expiracion")]
    public DateTime FechaExpiracion { get; set; }
}
