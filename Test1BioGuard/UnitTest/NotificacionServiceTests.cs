using MongoDB.Driver;
using Moq;
using BioGuard.Api.Config;
using BioGuard.Api.Services;
using BioGuard.Api.Models;
using FluentAssertions;

namespace Test1BioGuard.UnitTest;

public class NotificacionServiceTests
{
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly NotificacionService _service;
    private readonly Mock<IMongoCollection<Notificacion>> _mockCollection;

    public NotificacionServiceTests()
    {
        _mockDb = new Mock<IMongoDbContext>();
        _mockCollection = new Mock<IMongoCollection<Notificacion>>();
        _mockDb.Setup(db => db.Notificaciones).Returns(_mockCollection.Object);
        _service = new NotificacionService(_mockDb.Object);
    }

    [Fact]
    public async Task CrearAsync_DatosValidos_RetornaNotificacion()
    {
        _mockCollection.Setup(c => c.InsertOneAsync(
            It.IsAny<Notificacion>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CrearAsync("123456789012345678901234", "Alerta de glucosa", "Nivel alto detectado", "alerta");

        result.Should().NotBeNull();
        result.Titulo.Should().Be("Alerta de glucosa");
        result.Mensaje.Should().Be("Nivel alto detectado");
        result.Tipo.Should().Be("alerta");
        result.Leida.Should().BeFalse();
    }

    [Fact]
    public async Task MarcarLeidaAsync_NotificacionExiste_RetornaTrue()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(1);

        _mockCollection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Notificacion>>(),
            It.IsAny<UpdateDefinition<Notificacion>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.MarcarLeidaAsync("123456789012345678901234");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ObtenerPorPacienteAsync_ConNotificaciones_RetornaLista()
    {
        var notificaciones = new List<Notificacion>
        {
            new() { Titulo = "Alerta 1", Mensaje = "Mensaje 1", Leida = false },
            new() { Titulo = "Alerta 2", Mensaje = "Mensaje 2", Leida = true }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                _mockCollection.Object,
                It.IsAny<FilterDefinition<Notificacion>>(),
                It.IsAny<SortDefinition<Notificacion>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(notificaciones);

        var result = await _service.ObtenerPorPacienteAsync("123456789012345678901234");

        result.Should().NotBeEmpty();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ObtenerPorUsuarioAsync_ConNotificaciones_RetornaLista()
    {
        var notificaciones = new List<Notificacion>
        {
            new() { Titulo = "Notif 1", Mensaje = "Mensaje 1", Leida = false, UsuarioWebId = "user123" },
            new() { Titulo = "Notif 2", Mensaje = "Mensaje 2", Leida = true, UsuarioWebId = "user123" }
        };

        _mockDb.Setup(db => db.FindToListAsync(
                _mockCollection.Object,
                It.IsAny<FilterDefinition<Notificacion>>(),
                It.IsAny<SortDefinition<Notificacion>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(notificaciones);

        var result = await _service.ObtenerPorUsuarioAsync("user123");

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ObtenerPorUsuarioAsync_SinNotificaciones_RetornaListaVacia()
    {
        _mockDb.Setup(db => db.FindToListAsync(
                _mockCollection.Object,
                It.IsAny<FilterDefinition<Notificacion>>(),
                It.IsAny<SortDefinition<Notificacion>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<Notificacion>());

        var result = await _service.ObtenerPorUsuarioAsync("user_sin_notifs");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task EliminarAsync_NotificacionExiste_RetornaTrue()
    {
        var mockResult = new Mock<DeleteResult>();
        mockResult.Setup(r => r.DeletedCount).Returns(1);

        _mockCollection.Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Notificacion>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.EliminarAsync("n1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EliminarAsync_NotificacionNoExiste_RetornaFalse()
    {
        var mockResult = new Mock<DeleteResult>();
        mockResult.Setup(r => r.DeletedCount).Returns(0);

        _mockCollection.Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<Notificacion>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.EliminarAsync("nonexistent");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarcarLeidaAsync_NotificacionNoExiste_RetornaFalse()
    {
        var mockResult = new Mock<UpdateResult>();
        mockResult.Setup(r => r.ModifiedCount).Returns(0);

        _mockCollection.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<Notificacion>>(),
            It.IsAny<UpdateDefinition<Notificacion>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult.Object);

        var result = await _service.MarcarLeidaAsync("nonexistent");

        result.Should().BeFalse();
    }
}
