using MongoDB.Driver;
using Moq;
using Microsoft.Extensions.Logging;
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
        var mockLogger = new Mock<ILogger<PacienteService>>();
        _service = new PacienteService(_mockDb.Object, mockLogger.Object);
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

        var result = await _service.CrearPacienteAsync(usuarioWebId, new BioGuard.Api.DTOs.CrearPacienteRequest(nombre));

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

    [Fact]
    public async Task GetAllByUsuarioAsync_ConPacientes_RetornaLista()
    {
        var pacientes = new List<Paciente>
        {
            new() { Id = "123456789012345678901234", UsuarioWebId = "user123", Nombre = "Paciente 1" },
            new() { Id = "123456789012345678901235", UsuarioWebId = "user123", Nombre = "Paciente 2" }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(pacientes);

        var result = await _service.GetAllByUsuarioAsync("user123");

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllByUsuarioAsync_SinPacientes_RetornaListaVacia()
    {
        _mockDb.Setup(db => db.FindToListAsync(
                _mockCollection.Object,
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new List<Paciente>());

        var result = await _service.GetAllByUsuarioAsync("user_sin_pacientes");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateBiometriaAsync_DatosValidos_ActualizaBiometria()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockCollection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Paciente>>(),
            It.IsAny<UpdateDefinition<Paciente>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var request = new BioGuard.Api.DTOs.UpdateBiometriaRequest(
            Edad: 30, PesoKg: 75.5, EstaturaCm: 175.0,
            EsDiabetico: false, FamiliaresDiabetes: false, ActividadFisica: "Moderada",
            FechaNacimiento: new DateTime(1995, 5, 15), Sexo: "M");

        await _service.UpdateBiometriaAsync("123456789012345678901234", request);

        _mockCollection.Verify(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Paciente>>(),
            It.IsAny<UpdateDefinition<Paciente>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CrearPacienteAsync_ConDatosBiometricos_PersisteTodosLosCampos()
    {
        _mockCollection.Setup(c => c.InsertOneAsync(
                It.IsAny<Paciente>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new BioGuard.Api.DTOs.CrearPacienteRequest(
            Nombre: "Ana García",
            FechaNacimiento: new DateTime(1990, 3, 10),
            Edad: 34,
            PesoKg: 60.5,
            EstaturaCm: 165.0,
            Sexo: "F",
            EsDiabetico: true,
            FamiliaresDiabetes: true,
            ActividadFisica: "Moderada");

        var codigo = await _service.CrearPacienteAsync("user123", request);

        codigo.Should().NotBeNullOrEmpty();
        _mockCollection.Verify(c => c.InsertOneAsync(
            It.Is<Paciente>(p =>
                p.Nombre == "Ana García" &&
                p.FechaNacimiento == new DateTime(1990, 3, 10) &&
                p.Biometria.Edad == 34 &&
                p.Biometria.PesoKg == 60.5 &&
                p.Biometria.EstaturaCm == 165.0 &&
                p.Biometria.Sexo == "F" &&
                p.Biometria.EsDiabetico == true &&
                p.Biometria.FamiliaresDiabetes == true &&
                p.Biometria.ActividadFisica == "Moderada" &&
                p.PerfilCompletado == true),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CrearPacienteAsync_SoloNombre_PerfilNoCompletado()
    {
        _mockCollection.Setup(c => c.InsertOneAsync(
                It.IsAny<Paciente>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new BioGuard.Api.DTOs.CrearPacienteRequest(Nombre: "Solo Nombre");

        await _service.CrearPacienteAsync("user123", request);

        _mockCollection.Verify(c => c.InsertOneAsync(
            It.Is<Paciente>(p =>
                p.Nombre == "Solo Nombre" &&
                p.PerfilCompletado == false &&
                p.Biometria.Sexo == string.Empty),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
