using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using BioGuard.Api.Services;

namespace Test1BioGuard.UnitTest;

public class RiesgoMatrizTests
{
    private readonly RiesgoMetabolicoService _service;

    public RiesgoMatrizTests()
    {
        var mockDb = new Mock<IMongoDbContext>();
        var mockLogger = new Mock<ILogger<RiesgoMetabolicoService>>();
        var mockRiesgo = new Mock<IRiesgoService>();
        _service = new RiesgoMetabolicoService(mockDb.Object, mockLogger.Object, mockRiesgo.Object);
    }

    private static LecturaSensor Lectura(int pulso, double temp, double estres) => new()
    {
        PulsoBpm = pulso,
        TemperaturaC = temp,
        EstresPct = estres
    };

    [Fact]
    public void ClasificarPorMatriz_HipoglucemiaCompleta_RetornaHipoglucemia()
    {
        var lectura = Lectura(120, 34.5, 85);
        _service.ClasificarPorMatriz(lectura).Should().Be("Hipoglucemia");
    }

    [Fact]
    public void ClasificarPorMatriz_HiPerPulsoOmitido_NoClasificaHipo()
    {
        _service.ClasificarPorMatriz(Lectura(110, 34.5, 85)).Should().Be("Indeterminado");
    }

    [Fact]
    public void ClasificarPorMatriz_TemperaturaLimite_NoClasificaHipo()
    {
        _service.ClasificarPorMatriz(Lectura(120, 35.0, 85)).Should().Be("Indeterminado");
    }

    [Fact]
    public void ClasificarPorMatriz_EstresLimite_NoClasificaHipo()
    {
        _service.ClasificarPorMatriz(Lectura(120, 34.5, 80)).Should().Be("Indeterminado");
    }

    [Fact]
    public void ClasificarPorMatriz_HiperglucemiaCompleta_RetornaHiperglucemia()
    {
        _service.ClasificarPorMatriz(Lectura(100, 38.0, 70)).Should().Be("Hiperglucemia");
    }

    [Fact]
    public void ClasificarPorMatriz_HiperglucemiaBordeInferiorPulso_RetornaHiperglucemia()
    {
        _service.ClasificarPorMatriz(Lectura(95, 38.0, 60)).Should().Be("Hiperglucemia");
    }

    [Fact]
    public void ClasificarPorMatriz_HiperglucemiaBordeSuperiorPulso_RetornaHiperglucemia()
    {
        _service.ClasificarPorMatriz(Lectura(110, 38.0, 80)).Should().Be("Hiperglucemia");
    }

    [Fact]
    public void ClasificarPorMatriz_HiperglucemiaTemperaturaLimite_NoClasificaHiper()
    {
        _service.ClasificarPorMatriz(Lectura(100, 37.2, 70)).Should().Be("Indeterminado");
    }

    [Fact]
    public void ClasificarPorMatriz_HiperglucemiaEstresBajo_NoClasificaHiper()
    {
        _service.ClasificarPorMatriz(Lectura(100, 38.0, 59)).Should().Be("Indeterminado");
    }

    [Fact]
    public void ClasificarPorMatriz_HiperglucemiaEstresAlto_NoClasificaHiper()
    {
        _service.ClasificarPorMatriz(Lectura(100, 38.0, 81)).Should().Be("Indeterminado");
    }

    [Fact]
    public void ClasificarPorMatriz_OptimoCompleto_RetornaOptimo()
    {
        _service.ClasificarPorMatriz(Lectura(70, 36.3, 40)).Should().Be("Optimo");
    }

    [Fact]
    public void ClasificarPorMatriz_OptimoBordeInferior_RetornaOptimo()
    {
        _service.ClasificarPorMatriz(Lectura(60, 36.0, 49)).Should().Be("Optimo");
    }

    [Fact]
    public void ClasificarPorMatriz_OptimoBordeSuperior_RetornaOptimo()
    {
        _service.ClasificarPorMatriz(Lectura(80, 36.7, 49)).Should().Be("Optimo");
    }

    [Fact]
    public void ClasificarPorMatriz_OptimoEstresEnLimite_NoClasificaOptimo()
    {
        _service.ClasificarPorMatriz(Lectura(70, 36.3, 50)).Should().Be("Indeterminado");
    }

    [Fact]
    public void ClasificarPorMatriz_ValoresMixtos_RetornaIndeterminado()
    {
        _service.ClasificarPorMatriz(Lectura(100, 36.3, 40)).Should().Be("Indeterminado");
        _service.ClasificarPorMatriz(Lectura(120, 38.0, 40)).Should().Be("Indeterminado");
        _service.ClasificarPorMatriz(Lectura(70, 34.0, 90)).Should().Be("Indeterminado");
    }
}
