using System.Net;
using System.Text;
using System.Text.Json;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using BioGuard.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Test1BioGuard.UnitTest;

public class MLPredictionClientTests
{
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK);

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Responder(request));
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static MLPredictionClient CrearCliente(StubHttpMessageHandler handler, string baseUrl)
    {
        var options = new OptionsWrapper<MLOptions>(new MLOptions { BaseUrl = baseUrl, TimeoutSeconds = 15 });
        var logger = new Mock<ILogger<MLPredictionClient>>();
        return new MLPredictionClient(new HttpClient(handler), options, logger.Object);
    }

    private static List<LecturaSensor> CrearLecturas(int count)
    {
        var lecturas = new List<LecturaSensor>();
        for (var i = 0; i < count; i++)
        {
            lecturas.Add(new LecturaSensor
            {
                Id = $"l{i}",
                Meta = new MetaData { PacienteId = "pac123" },
                Timestamp = DateTime.UtcNow.AddMinutes(-count + i),
                PulsoBpm = 80 + i,
                TemperaturaC = 36.5,
                SudoracionGsr = 3.0,
                ProbabilidadPico = 0.1
            });
        }
        return lecturas;
    }

    private static HttpResponseMessage RespuestaOk()
    {
        var body = new
        {
            paciente_id = "pac123",
            probabilidad_pico = 0.85,
            nivel_riesgo = "Pre-Pico",
            horas_estimadas = 2,
            recomendacion = "Mantener hidratación",
            modelo_version = "fallback-v1",
            fecha_prediccion = "2026-08-04T05:00:00Z",
            fecha_expiracion = "2026-08-04T07:00:00Z"
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };
    }

    [Fact]
    public async Task PredecirAsync_Configurado_RetornaPrediccion()
    {
        var handler = new StubHttpMessageHandler { Responder = _ => RespuestaOk() };
        var client = CrearCliente(handler, "http://ml.local");

        var resultado = await client.PredecirAsync("pac123", CrearLecturas(6));

        resultado.Should().NotBeNull();
        resultado!.PacienteId.Should().Be("pac123");
        resultado.ProbabilidadPico.Should().Be(0.85);
        resultado.NivelRiesgo.Should().Be("Pre-Pico");
        resultado.HorasEstimadas.Should().Be(2);
        resultado.ModeloVersion.Should().Be("fallback-v1");
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().EndWith("/api/v1/predicciones");
    }

    [Fact]
    public async Task PredecirAsync_ErrorHttp_RetornaNull()
    {
        var handler = new StubHttpMessageHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        };
        var client = CrearCliente(handler, "http://ml.local");

        var resultado = await client.PredecirAsync("pac123", CrearLecturas(6));

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task PredecirAsync_RespuestaInvalida_RetornaNull()
    {
        var handler = new StubHttpMessageHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("no-json", Encoding.UTF8, "application/json")
            }
        };
        var client = CrearCliente(handler, "http://ml.local");

        var resultado = await client.PredecirAsync("pac123", CrearLecturas(6));

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task PredecirAsync_NoConfigurado_Lanza()
    {
        var client = CrearCliente(new StubHttpMessageHandler(), "");

        var act = () => client.PredecirAsync("pac123", CrearLecturas(6));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ML:BaseUrl*");
    }

    [Fact]
    public async Task PredecirAsync_SinLecturas_Lanza()
    {
        var client = CrearCliente(new StubHttpMessageHandler(), "http://ml.local");

        var act = () => client.PredecirAsync("pac123", new List<LecturaSensor>());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No hay lecturas*");
    }
}
