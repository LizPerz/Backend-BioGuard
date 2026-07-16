using MongoDB.Driver;
using Moq;
using BioGuard.Api.Config;
using BioGuard.Api.Services;
using BioGuard.Api.Models;
using FluentAssertions;

namespace Test1BioGuard.UnitTest;

public class AuditoriaServiceTests
{
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly AuditoriaService _service;
    private readonly Mock<IMongoCollection<Auditoria>> _mockAuditoria;

    public AuditoriaServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockAuditoria = new Mock<IMongoCollection<Auditoria>>();
        _mockDb.Setup(db => db.Auditoria).Returns(_mockAuditoria.Object);
        _service = new AuditoriaService(_mockDb.Object);
    }

    [Fact]
    public async Task ObtenerAsync_ConRegistros_RetornaLista()
    {
        var registros = new List<Auditoria>
        {
            new() { Id = "aud1", Accion = "Login", Fecha = DateTime.UtcNow }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Auditoria>>(),
                It.IsAny<FilterDefinition<Auditoria>>(),
                It.IsAny<SortDefinition<Auditoria>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(registros);

        var result = await _service.ObtenerAsync(1, 50);

        result.Should().HaveCount(1);
        result[0].Accion.Should().Be("Login");
    }

    [Fact]
    public async Task ObtenerAsync_SinRegistros_RetornaListaVacia()
    {
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Auditoria>>(),
                It.IsAny<FilterDefinition<Auditoria>>(),
                It.IsAny<SortDefinition<Auditoria>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<Auditoria>());

        var result = await _service.ObtenerAsync(1, 50);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RegistrarAsync_DatosValidos_Registra()
    {
        _mockAuditoria.Setup(c => c.InsertOneAsync(
            It.IsAny<Auditoria>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.RegistrarAsync("user1", "Login", "usuarios_web", "user1", "127.0.0.1");

        _mockAuditoria.Verify(c => c.InsertOneAsync(
            It.IsAny<Auditoria>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
