using MongoDB.Driver;
using Moq;
using BioGuard.Api.Config;
using BioGuard.Api.Services;
using BioGuard.Api.Models;
using FluentAssertions;

namespace Test1BioGuard.UnitTest;

public class PacienteServiceTests
{
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly PacienteService _service;
    private readonly Mock<IMongoCollection<Paciente>> _mockCollection;

    public PacienteServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockCollection = new Mock<IMongoCollection<Paciente>>();
        _mockDb.Setup(db => db.Pacientes).Returns(_mockCollection.Object);
        _service = new PacienteService(_mockDb.Object);
    }

    [Fact]
    public async Task GetByIdAsync_PacienteExiste_RetornaPaciente()
    {
        var pacienteId = "123456789012345678901234";
        var paciente = new Paciente
        {
            Id = pacienteId,
            Nombre = "Juan Pérez",
            CodigoAccesoQr = "ABC12345",
            UsuarioWebId = "user123",
            FechaRegistro = DateTime.UtcNow
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(paciente);

        var result = await _service.GetByIdAsync(pacienteId);

        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Juan Pérez");
        result.CodigoAccesoQr.Should().Be("ABC12345");
    }

    [Fact]
    public async Task GetByIdAsync_PacienteNoExiste_RetornaNull()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync((Paciente?)null);

        var result = await _service.GetByIdAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByCodigoAsync_CodigoValido_RetornaPaciente()
    {
        var codigo = "ABC12345";
        var paciente = new Paciente
        {
            Id = "123456789012345678901234",
            CodigoAccesoQr = codigo,
            Nombre = "María García"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(paciente);

        var result = await _service.GetByCodigoAsync(codigo);

        result.Should().NotBeNull();
        result!.CodigoAccesoQr.Should().Be(codigo);
    }

    [Fact]
    public async Task CrearPacienteAsync_DatosValidos_RetornaCodigo()
    {
        var usuarioWebId = "user123";
        var nombre = "Nuevo Paciente";

        _mockCollection.Setup(c => c.InsertOneAsync(
            It.IsAny<Paciente>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CrearPacienteAsync(usuarioWebId, nombre);

        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(8);
        _mockCollection.Verify(c => c.InsertOneAsync(
            It.IsAny<Paciente>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateNombreAsync_PacienteExiste_RetornaTrue()
    {
        var pacienteId = "123456789012345678901234";
        var nuevoNombre = "Nombre Actualizado";

        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockCollection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Paciente>>(),
            It.IsAny<UpdateDefinition<Paciente>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.UpdateNombreAsync(pacienteId, nuevoNombre);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EliminarAsync_PacienteExiste_RetornaTrue()
    {
        var pacienteId = "123456789012345678901234";

        var mockDeleteResult = new Mock<DeleteResult>();
        mockDeleteResult.Setup(r => r.DeletedCount).Returns(1);

        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<LecturaSensor>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<LecturaSensor, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<EventoMetabolico>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<EventoMetabolico, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<TrackingGps>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<TrackingGps, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<Notificacion>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Notificacion, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<Dispositivo>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Dispositivo, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<Medicamento>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Medicamento, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<Alerta>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Alerta, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);

        _mockCollection.Setup(c => c.DeleteOneAsync(
            It.IsAny<FilterDefinition<Paciente>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeleteResult.Object);

        var result = await _service.EliminarAsync(pacienteId);

        result.Should().BeTrue();
    }
}
