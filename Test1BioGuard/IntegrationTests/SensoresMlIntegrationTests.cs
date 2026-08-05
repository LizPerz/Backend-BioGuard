using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;
using BioGuard.Api.Services;

namespace Test1BioGuard.IntegrationTests;

public abstract class MlStubWebApplicationFactory : CustomWebApplicationFactory
{
    private readonly bool _responderConError;

    protected MlStubWebApplicationFactory(bool responderConError)
    {
        _responderConError = responderConError;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            var mlDescriptors = services
                .Where(d => d.ServiceType == typeof(MLPredictionClient))
                .ToList();
            foreach (var descriptor in mlDescriptors) services.Remove(descriptor);

            services.Configure<MLOptions>(o =>
            {
                o.BaseUrl = "http://ml.test";
                o.TimeoutSeconds = 15;
            });

            services.AddHttpClient<MLPredictionClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new StubMlMessageHandler(_responderConError));
        });
    }
}

public class MlConfiguredWebApplicationFactory : MlStubWebApplicationFactory
{
    public MlConfiguredWebApplicationFactory() : base(false) { }
}

public class MlErrorWebApplicationFactory : MlStubWebApplicationFactory
{
    public MlErrorWebApplicationFactory() : base(true) { }
}

public class StubMlMessageHandler : HttpMessageHandler
{
    private readonly bool _responderConError;

    public StubMlMessageHandler(bool responderConError)
    {
        _responderConError = responderConError;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var status = _responderConError ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
        var body = _responderConError
            ? "error interno"
            : "{\"paciente_id\":\"123456789012345678901234\",\"probabilidad_pico\":0.93,\"nivel_riesgo\":\"Critico\",\"horas_estimadas\":4,\"recomendacion\":\"Monitoreo continuo\",\"modelo_version\":\"baseline-v0\",\"fecha_prediccion\":\"2026-08-05T00:00:00Z\",\"fecha_expiracion\":\"2026-08-05T02:00:00Z\"}";

        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

public class SensoresMlIntegrationTests :
    IClassFixture<MlConfiguredWebApplicationFactory>,
    IClassFixture<MlErrorWebApplicationFactory>
{
    private readonly HttpClient _clientOk;
    private readonly HttpClient _clientError;
    private readonly Mock<IMongoDbContext> _mockDbOk;
    private readonly Mock<IMongoDbContext> _mockDbError;
    private readonly Mock<IMongoCollection<LecturaSensor>> _mockLecturasOk;
    private readonly Mock<IMongoCollection<LecturaSensor>> _mockLecturasError;

    public SensoresMlIntegrationTests(
        MlConfiguredWebApplicationFactory okFactory,
        MlErrorWebApplicationFactory errorFactory)
    {
        _clientOk = okFactory.CreateClient();
        _clientError = errorFactory.CreateClient();
        _mockDbOk = okFactory.MockDbContext;
        _mockDbError = errorFactory.MockDbContext;

        _mockLecturasOk = new Mock<IMongoCollection<LecturaSensor>>();
        _mockLecturasError = new Mock<IMongoCollection<LecturaSensor>>();

        ConfigurarDbContext(_mockDbOk, _mockLecturasOk);
        ConfigurarDbContext(_mockDbError, _mockLecturasError);
    }

    private static void ConfigurarDbContext(Mock<IMongoDbContext> mockDb, Mock<IMongoCollection<LecturaSensor>> mockLecturas)
    {
        mockDb.Setup(db => db.LecturasSensores).Returns(mockLecturas.Object);
        mockDb.Setup(db => db.Pacientes).Returns(new Mock<IMongoCollection<Paciente>>().Object);

        mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new Paciente
            {
                Id = "123456789012345678901234",
                UsuarioWebId = "user123",
                Nombre = "Paciente Test"
            });

        mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new List<Paciente>
            {
                new()
                {
                    Id = "123456789012345678901234",
                    UsuarioWebId = "user123",
                    Nombre = "Paciente Test"
                }
            });
    }

    private static void ConfigurarHistorialVacio(Mock<IMongoDbContext> mockDb)
    {
        mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<LecturaSensor>>(),
                It.IsAny<FilterDefinition<LecturaSensor>>(),
                It.IsAny<SortDefinition<LecturaSensor>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<LecturaSensor>());
    }

    [Fact]
    public async Task RecibirLectura_MLDisponible_GuardaRiesgoCalculado()
    {
        var pacienteId = "123456789012345678901234";
        ConfigurarHistorialVacio(_mockDbOk);

        LecturaSensor? insertada = null;
        _mockLecturasOk.Setup(c => c.InsertOneAsync(
                It.IsAny<LecturaSensor>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<LecturaSensor, InsertOneOptions?, CancellationToken>((lectura, _, _) => insertada = lectura)
            .Returns(Task.CompletedTask);

        _clientOk.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.GeneratePacienteToken(pacienteId));

        var request = new LecturaSensorRequest(95, 37.2, 18.0);
        var response = await _clientOk.PostAsJsonAsync("/api/Sensores/lectura", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        insertada.Should().NotBeNull();
        insertada!.ProbabilidadPico.Should().BeApproximately(0.93, 0.001);
    }

    [Fact]
    public async Task RecibirLectura_MLError_GuardaRiesgoCero()
    {
        var pacienteId = "123456789012345678901234";
        ConfigurarHistorialVacio(_mockDbError);

        LecturaSensor? insertada = null;
        _mockLecturasError.Setup(c => c.InsertOneAsync(
                It.IsAny<LecturaSensor>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<LecturaSensor, InsertOneOptions?, CancellationToken>((lectura, _, _) => insertada = lectura)
            .Returns(Task.CompletedTask);

        _clientError.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.GeneratePacienteToken(pacienteId));

        var request = new LecturaSensorRequest(95, 37.2, 18.0);
        var response = await _clientError.PostAsJsonAsync("/api/Sensores/lectura", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        insertada.Should().NotBeNull();
        insertada!.ProbabilidadPico.Should().Be(0.0);
    }

    [Fact]
    public async Task RecibirLecturaBatch_MLDisponible_GuardaRiesgoCalculado()
    {
        var pacienteId = "123456789012345678901234";
        ConfigurarHistorialVacio(_mockDbOk);

        var insertadas = new List<LecturaSensor>();
        _mockLecturasOk.Setup(c => c.InsertOneAsync(
                It.IsAny<LecturaSensor>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<LecturaSensor, InsertOneOptions?, CancellationToken>((lectura, _, _) => insertadas.Add(lectura))
            .Returns(Task.CompletedTask);

        _clientOk.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.GeneratePacienteToken(pacienteId));

        var request = new List<LecturaSensorRequest>
        {
            new(95, 37.2, 18.0),
            new(80, 36.8, 14.0)
        };
        var response = await _clientOk.PostAsJsonAsync("/api/Sensores/lectura-batch", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("procesadas").GetInt32().Should().Be(2);

        insertadas.Should().HaveCount(2);
        insertadas.Should().OnlyContain(l => Math.Abs(l.ProbabilidadPico - 0.93) < 0.001);
    }
}
