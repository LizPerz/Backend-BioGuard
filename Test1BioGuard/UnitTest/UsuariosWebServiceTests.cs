using MongoDB.Driver;
using Moq;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Config;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;
using FluentAssertions;

namespace Test1BioGuard.UnitTest;

public class UsuariosWebServiceTests
{
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly UsuariosWebService _service;
    private readonly Mock<IMongoCollection<UsuarioWeb>> _mockUsuarios;
    private readonly Mock<IMongoCollection<Plan>> _mockPlanes;
    private readonly Mock<IMongoCollection<Paciente>> _mockPacientes;
    private readonly Mock<IMongoCollection<Cuidador>> _mockCuidadores;
    private readonly Mock<IMongoCollection<Pago>> _mockPagos;

    public UsuariosWebServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockUsuarios = new Mock<IMongoCollection<UsuarioWeb>>();
        _mockPlanes = new Mock<IMongoCollection<Plan>>();
        _mockPacientes = new Mock<IMongoCollection<Paciente>>();
        _mockCuidadores = new Mock<IMongoCollection<Cuidador>>();
        _mockPagos = new Mock<IMongoCollection<Pago>>();

        _mockDb.Setup(db => db.UsuariosWeb).Returns(_mockUsuarios.Object);
        _mockDb.Setup(db => db.Planes).Returns(_mockPlanes.Object);
        _mockDb.Setup(db => db.Pacientes).Returns(_mockPacientes.Object);
        _mockDb.Setup(db => db.Cuidadores).Returns(_mockCuidadores.Object);
        _mockDb.Setup(db => db.Pagos).Returns(_mockPagos.Object);

        var mockLogger = new Mock<ILogger<UsuariosWebService>>();
        _service = new UsuariosWebService(_mockDb.Object, mockLogger.Object);
    }

    [Fact]
    public async Task GetByIdAsync_UsuarioExiste_RetornaUsuario()
    {
        var usuario = new UsuarioWeb { Id = "user123", Nombre = "Juan", Correo = "juan@test.com" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(usuario);

        var result = await _service.GetByIdAsync("user123");

        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Juan");
    }

    [Fact]
    public async Task GetByIdAsync_UsuarioNoExiste_RetornaNull()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);

        var result = await _service.GetByIdAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPlanAsync_UsuarioConPlan_RetornaPlan()
    {
        var usuario = new UsuarioWeb { Id = "user123", PlanId = "plan1" };
        var plan = new Plan { Id = "plan1", Nombre = "Premium", Precio = 9.99m };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(usuario);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        var result = await _service.GetPlanAsync("user123");

        result.Should().NotBeNull();
        result!.Nombre.Should().Be("Premium");
    }

    [Fact]
    public async Task GetPlanAsync_UsuarioNoExiste_RetornaNull()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);

        var result = await _service.GetPlanAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePerfilAsync_DatosValidos_RetornaTrue()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var request = new UpdatePerfilRequest("Juan", "Perez", "Lopez");
        var result = await _service.UpdatePerfilAsync("user123", request);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CambiarCorreoAsync_CorreoNoExiste_RetornaTrue()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.CambiarCorreoAsync("user123", "nuevo@test.com");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CambiarCorreoAsync_CorreoYaExiste_RetornaFalse()
    {
        var existente = new UsuarioWeb { Id = "other", Correo = "nuevo@test.com" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(existente);

        var result = await _service.CambiarCorreoAsync("user123", "nuevo@test.com");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SubirFotoAsync_DatosValidos_RetornaTrue()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.SubirFotoAsync("user123", "base64data");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CambiarPlanAsync_PlanExiste_RetornaTrue()
    {
        var plan = new Plan { Id = "plan2", Nombre = "Pro" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.CambiarPlanAsync("user123", "Pro");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CambiarPlanAsync_PlanNoExiste_RetornaFalse()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync((Plan?)null);

        var result = await _service.CambiarPlanAsync("user123", "Inexistente");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EliminarCuentaAsync_UsuarioExiste_RetornaTrue()
    {
        var mockDeleteResult = new Mock<DeleteResult>();
        mockDeleteResult.Setup(r => r.DeletedCount).Returns(1);

        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<Cuidador>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.FindToListAsync(It.IsAny<IMongoCollection<Paciente>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(new List<Paciente>());
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<Paciente>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockDb.Setup(db => db.DeleteManyAsync(It.IsAny<IMongoCollection<Pago>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Pago, bool>>>()))
            .ReturnsAsync(mockDeleteResult.Object);
        _mockUsuarios.Setup(c => c.DeleteOneAsync(
            It.IsAny<FilterDefinition<UsuarioWeb>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeleteResult.Object);

        var result = await _service.EliminarCuentaAsync("user123");

        result.Should().BeTrue();
    }
}
