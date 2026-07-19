using MongoDB.Driver;
using Moq;
using BioGuard.Api.Config;
using BioGuard.Api.Services;
using BioGuard.Api.Models;
using FluentAssertions;

namespace Test1BioGuard.UnitTest;

public class CuidadorServiceTests
{
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly CuidadorService _service;
    private readonly Mock<IMongoCollection<Cuidador>> _mockCollection;

    public CuidadorServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockCollection = new Mock<IMongoCollection<Cuidador>>();
        _mockDb.Setup(db => db.Cuidadores).Returns(_mockCollection.Object);
        _service = new CuidadorService(_mockDb.Object);
    }

    [Fact]
    public async Task CrearAsync_DatosValidos_RetornaCuidadorYCodigo()
    {
        _mockCollection.Setup(c => c.InsertOneAsync(
            It.IsAny<Cuidador>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (cuidador, codigo) = await _service.CrearAsync(
            "user123", "paciente123", "María García", "Madre", "5551234567", "maria@email.com");

        cuidador.Should().NotBeNull();
        cuidador!.Nombre.Should().Be("María García");
        cuidador.Parentesco.Should().Be("Madre");
        codigo.Should().StartWith("CU-");
        codigo.Should().HaveLength(11);
    }

    [Fact]
    public async Task ContarPorPacienteAsync_ConCuidadores_RetornaCantidad()
    {
        _mockDb.Setup(db => db.CountDocumentsAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(2);

        var result = await _service.ContarPorPacienteAsync("paciente123");

        result.Should().Be(2);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_CuidadorExiste_RetornaCuidador()
    {
        var cuidador = new Cuidador
        {
            Id = "123456789012345678901234",
            Nombre = "María García",
            Parentesco = "Madre",
            PacienteId = "paciente123",
            CodigoAccesoQr = "CU-ABC123"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(cuidador);

        var result = await _service.ObtenerPorIdAsync(cuidador.Id);

        result.Should().NotBeNull();
        result!.Nombre.Should().Be("María García");
    }

    [Fact]
    public async Task EliminarAsync_CuidadorExiste_RetornaTrue()
    {
        var mockResult = new Mock<DeleteResult>();
        mockResult.Setup(r => r.DeletedCount).Returns(1);

        _mockCollection.Setup(c => c.DeleteOneAsync(
            It.IsAny<FilterDefinition<Cuidador>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.EliminarAsync("123456789012345678901234");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RegenerarQRAsync_CuidadorExiste_RetornaNuevoCodigo()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockCollection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Cuidador>>(),
            It.IsAny<UpdateDefinition<Cuidador>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.RegenerarQRAsync("123456789012345678901234");

        result.Should().StartWith("CU-");
        result.Should().HaveLength(11);
    }

    [Fact]
    public async Task ObtenerPorUsuarioAsync_ConCuidadores_RetornaLista()
    {
        var cuidadores = new List<Cuidador>
        {
            new() { Id = "c1", UsuarioWebId = "user123", Nombre = "María García", PacienteId = "pac1" },
            new() { Id = "c2", UsuarioWebId = "user123", Nombre = "Carlos López", PacienteId = "pac1" }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(cuidadores);

        var result = await _service.ObtenerPorUsuarioAsync("user123");

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ObtenerPorUsuarioAsync_SinCuidadores_RetornaListaVacia()
    {
        _mockDb.Setup(db => db.FindToListAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(new List<Cuidador>());

        var result = await _service.ObtenerPorUsuarioAsync("user_sin_cuidadores");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerPorPacienteAsync_ConCuidadores_RetornaLista()
    {
        var cuidadores = new List<Cuidador>
        {
            new() { Id = "c1", PacienteId = "pac1", Nombre = "María García" }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(cuidadores);

        var result = await _service.ObtenerPorPacienteAsync("pac1");

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task ObtenerPorPacienteAsync_SinCuidadores_RetornaListaVacia()
    {
        _mockDb.Setup(db => db.FindToListAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(new List<Cuidador>());

        var result = await _service.ObtenerPorPacienteAsync("pac_sin_cuidadores");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerPorIdAsync_CuidadorNoExiste_RetornaNull()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync((Cuidador?)null);

        var result = await _service.ObtenerPorIdAsync("nonexistent");

        result.Should().BeNull();
    }
}
