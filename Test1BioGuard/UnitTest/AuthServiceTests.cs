using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using BioGuard.Api.Config;
using BioGuard.Api.Services;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;
using FluentAssertions;
using MongoDB.Driver;

namespace Test1BioGuard.UnitTest;

public class AuthServiceTests
{
    private readonly Mock<IMongoDbContext> _mockDb;
    private readonly AuthService _service;
    private readonly Mock<IMongoCollection<UsuarioWeb>> _mockUsuarios;
    private readonly Mock<IMongoCollection<Plan>> _mockPlanes;
    private readonly Mock<IMongoCollection<Paciente>> _mockPacientes;
    private readonly Mock<IMongoCollection<Cuidador>> _mockCuidadores;

    private readonly Mock<IMongoCollection<RefreshToken>> _mockRefreshTokens;

    public AuthServiceTests()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "BioGuard2024SecretKeyForJWTAuthentication!@#$%^&*()");

        _mockDb = new Mock<IMongoDbContext>();
        _mockUsuarios = new Mock<IMongoCollection<UsuarioWeb>>();
        _mockPlanes = new Mock<IMongoCollection<Plan>>();
        _mockPacientes = new Mock<IMongoCollection<Paciente>>();
        _mockCuidadores = new Mock<IMongoCollection<Cuidador>>();
        _mockRefreshTokens = new Mock<IMongoCollection<RefreshToken>>();

        _mockDb.Setup(db => db.UsuariosWeb).Returns(_mockUsuarios.Object);
        _mockDb.Setup(db => db.Planes).Returns(_mockPlanes.Object);
        _mockDb.Setup(db => db.Pacientes).Returns(_mockPacientes.Object);
        _mockDb.Setup(db => db.Cuidadores).Returns(_mockCuidadores.Object);
        _mockDb.Setup(db => db.RefreshTokens).Returns(_mockRefreshTokens.Object);

        _mockRefreshTokens.Setup(c => c.InsertOneAsync(
            It.IsAny<RefreshToken>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "BioGuard2024SecretKeyForJWTAuthentication!@#$%^&*()",
            ["Jwt:Issuer"] = "BioGuardApi",
            ["Jwt:Audience"] = "BioGuardApp",
            ["Jwt:ExpirationMinutes"] = "1440"
        }).Build();

        var mockLogger = new Mock<ILogger<AuthService>>();
        var mockEmailService = new Mock<IEmailService>();
        _service = new AuthService(_mockDb.Object, config, new HttpClient(), mockLogger.Object, mockEmailService.Object);
    }

    [Fact]
    public async Task RegisterWebAsync_DatosValidos_RetornaAuthResponse()
    {
        var plan = new Plan { Id = "plan1", Nombre = "Premium" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);
        _mockUsuarios.Setup(c => c.InsertOneAsync(
            It.IsAny<UsuarioWeb>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new RegisterWebRequest("Juan", "Perez", "juan@test.com", "Password123!", "Premium", "Lopez");
        var result = await _service.RegisterWebAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().BeNull();
        result.Response!.RequiresVerification.Should().BeTrue();
        result.Response.Rol.Should().Be("dueno");
        result.Response.Plan.Should().Be("Premium");
    }

    [Fact]
    public async Task RegisterWebAsync_CorreoExistente_RetornaError()
    {
        var existing = new UsuarioWeb { Correo = "juan@test.com", Activo = true };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(existing);

        var request = new RegisterWebRequest("Juan", "Perez", "juan@test.com", "Password123!", "Premium", "Lopez");
        var result = await _service.RegisterWebAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().Be("El correo ya está registrado");
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task RegisterWebAsync_CorreoExistenteCaseInsensitive_RetornaError()
    {
        var existing = new UsuarioWeb { Correo = "juan@test.com", Activo = true };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(existing);

        var request = new RegisterWebRequest("Juan", "Perez", "JUAN@TEST.COM", "Password123!", "Premium", "Lopez");
        var result = await _service.RegisterWebAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().Be("El correo ya está registrado");
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task RegisterWebAsync_NombreSoloEspacios_RetornaError()
    {
        var request = new RegisterWebRequest("   ", "Perez", "juan@test.com", "Password123!", "Premium", "Lopez");
        var result = await _service.RegisterWebAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().Be("El nombre es obligatorio");
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task RegisterWebAsync_ApellidoConDigitos_RetornaError()
    {
        var request = new RegisterWebRequest("Juan", "Perez123", "juan@test.com", "Password123!", "Premium", "Lopez");
        var result = await _service.RegisterWebAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().Be("El apellido solo puede contener letras y espacios");
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task RegisterWebAsync_NormalizaCorreoYNombre()
    {
        var plan = new Plan { Id = "plan1", Nombre = "Premium" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        UsuarioWeb? inserted = null;
        _mockUsuarios.Setup(c => c.InsertOneAsync(
                It.IsAny<UsuarioWeb>(),
                It.IsAny<InsertOneOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<UsuarioWeb, InsertOneOptions, CancellationToken>((u, _, _) => inserted = u)
            .Returns(Task.CompletedTask);

        var request = new RegisterWebRequest("  Juan  ", "De la Cruz", "  JUAN@TEST.COM  ", "Password123!", "Premium", "Lopez");
        var result = await _service.RegisterWebAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().BeNull();
        inserted.Should().NotBeNull();
        inserted!.Correo.Should().Be("juan@test.com");
        inserted.Nombre.Should().Be("Juan");
        inserted.ApellidoPaterno.Should().Be("De la Cruz");
    }

    [Fact]
    public async Task RegisterWebAsync_PlanNoExiste_RetornaError()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync((Plan?)null);

        var request = new RegisterWebRequest("Juan", "Perez", "juan@test.com", "Password123!", "Inexistente", "Lopez");
        var result = await _service.RegisterWebAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().Be("El plan seleccionado no existe");
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task RegisterWebAsync_PasswordDebil_RetornaError()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(new Plan { Id = "plan1", Nombre = "Premium" });

        var request = new RegisterWebRequest("Juan", "Perez", "juan@test.com", "PASSWORD123!", "Premium", "Lopez");
        var result = await _service.RegisterWebAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().Contain("minúscula");
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task LoginWebAsync_CredencialesValidas_RetornaAuthResponse()
    {
        var user = new UsuarioWeb
        {
            Id = "user123", Correo = "test@test.com", Activo = true,
            PasswordHash = PasswordHasher.Hash("Password123!"),
            PlanId = "plan1", Nombre = "Test", ApellidoPaterno = "User"
        };
        var plan = new Plan { Id = "plan1", Nombre = "Premium" };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        var request = new LoginWebRequest("test@test.com", "Password123!");
        var result = await _service.LoginWebAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().BeNull();
        result.Response!.Token.Should().NotBeNullOrEmpty();
        result.Response.Rol.Should().Be("dueno");
    }

    [Fact]
    public async Task LoginWebAsync_CredencialesInvalidas_RetornaError()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);

        var request = new LoginWebRequest("wrong@test.com", "WrongPass123!");
        var result = await _service.LoginWebAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().Be("Credenciales inválidas");
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task LoginWebAsync_UsuarioInactivo_RetornaErrorVerificacion()
    {
        var user = new UsuarioWeb
        {
            Id = "user123", Correo = "test@test.com", Activo = false,
            PasswordHash = PasswordHasher.Hash("Password123!")
        };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);

        var request = new LoginWebRequest("test@test.com", "Password123!");
        var result = await _service.LoginWebAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().Contain("verificado");
        result.Response.Should().BeNull();
    }

    [Fact]
    public async Task LoginWebAsync_EsAdmin_RetornaRolAdmin()
    {
        var user = new UsuarioWeb
        {
            Id = "admin123", Correo = "admin@test.com", Activo = true, EsAdmin = true,
            PasswordHash = PasswordHasher.Hash("Password123!"),
            PlanId = "plan1", Nombre = "Admin", ApellidoPaterno = "Sistema"
        };
        var plan = new Plan { Id = "plan1", Nombre = "Premium" };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        var request = new LoginWebRequest("admin@test.com", "Password123!");
        var result = await _service.LoginWebAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().BeNull();
        result.Response!.Rol.Should().Be("admin");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Response.Token);
        jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value.Should().Be("admin");
    }

    [Fact]
    public async Task LoginWebAsync_DuenoNormal_NoEmiteRolAdmin()
    {
        var user = new UsuarioWeb
        {
            Id = "user123", Correo = "test@test.com", Activo = true, EsAdmin = false,
            PasswordHash = PasswordHasher.Hash("Password123!"),
            PlanId = "plan1", Nombre = "Test", ApellidoPaterno = "User"
        };
        var plan = new Plan { Id = "plan1", Nombre = "Premium" };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        var request = new LoginWebRequest("test@test.com", "Password123!");
        var result = await _service.LoginWebAsync(request);

        result.Should().NotBeNull();
        result.Error.Should().BeNull();
        result.Response!.Rol.Should().Be("dueno");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Response.Token);
        jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value.Should().Be("dueno");
    }

    [Fact]
    public async Task LoginGoogle_UsuarioInactivo_RetornaNull()
    {
        var user = new UsuarioWeb
        {
            Id = "user123", Correo = "inactivo@test.com", Activo = false, PlanId = "plan1"
        };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);

        var result = await _service.LoginGoogleValidadoAsync("inactivo@test.com", "sub123");

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginGoogle_UsuarioActivo_RetornaAuthResponse()
    {
        var user = new UsuarioWeb
        {
            Id = "user123", Correo = "activo@test.com", Activo = true, EsAdmin = true,
            PlanId = "plan1", Nombre = "Juan", ApellidoPaterno = "Perez"
        };
        var plan = new Plan { Id = "plan1", Nombre = "Premium" };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);

        var result = await _service.LoginGoogleValidadoAsync("activo@test.com", "sub123");

        result.Should().NotBeNull();
        result!.Rol.Should().Be("admin");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value.Should().Be("admin");
    }

    [Fact]
    public async Task LoginByCodigoAsync_CodigoPaciente_RetornaAuthResponse()
    {
        var paciente = new Paciente { Id = "pac123", CodigoAccesoQr = "ABC12345", Nombre = "Paciente" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(paciente);

        var request = new LoginCodigoRequest("ABC12345");
        var result = await _service.LoginByCodigoAsync(request);

        result.Should().NotBeNull();
        result!.Rol.Should().Be("paciente");
        result.Nombre.Should().Be("Paciente");
    }

    [Fact]
    public async Task LoginByCodigoAsync_CodigoCuidador_RetornaAuthResponse()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync((Paciente?)null);
        var cuidador = new Cuidador { Id = "cuid123", CodigoAccesoQr = "CU-ABC123", Nombre = "Cuidador" };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(cuidador);

        var request = new LoginCodigoRequest("CU-ABC123");
        var result = await _service.LoginByCodigoAsync(request);

        result.Should().NotBeNull();
        result!.Rol.Should().Be("cuidador");
    }

    [Fact]
    public async Task LoginByCodigoAsync_CodigoInvalido_RetornaNull()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync((Paciente?)null);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync((Cuidador?)null);

        var request = new LoginCodigoRequest("INVALID");
        var result = await _service.LoginByCodigoAsync(request);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Enviar2FAAsync_UsuarioExiste_RetornaTrue()
    {
        var user = new UsuarioWeb { Id = "user123", Correo = "test@test.com", Activo = true };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<UpdateResult>().Object);

        var request = new Enviar2FARequest("test@test.com");
        var result = await _service.Enviar2FAAsync(request);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Enviar2FAAsync_UsuarioNoExiste_RetornaFalse()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);

        var request = new Enviar2FARequest("noexist@test.com");
        var result = await _service.Enviar2FAAsync(request);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Verificar2FAAsync_CodigoValido_RetornaAuthResponse()
    {
        var user = new UsuarioWeb
        {
            Id = "user123", Correo = "test@test.com", Activo = true,
            TwoFactorCode = "123456", TwoFactorExpira = DateTime.UtcNow.AddMinutes(5),
            PlanId = "plan1", Nombre = "Test", ApellidoPaterno = "User"
        };
        var plan = new Plan { Id = "plan1", Nombre = "Premium" };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync(plan);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<UpdateResult>().Object);

        var request = new Verificar2FARequest("test@test.com", "123456");
        var result = await _service.Verificar2FAAsync(request);

        result.Should().NotBeNull();
        result!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Verificar2FAAsync_CodigoInvalido_RetornaNull()
    {
        var user = new UsuarioWeb
        {
            Id = "user123", Correo = "test@test.com", Activo = true,
            TwoFactorCode = "123456", TwoFactorExpira = DateTime.UtcNow.AddMinutes(5)
        };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);

        var request = new Verificar2FARequest("test@test.com", "999999");
        var result = await _service.Verificar2FAAsync(request);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ForgotPasswordAsync_UsuarioExiste_RetornaTrue()
    {
        var user = new UsuarioWeb { Id = "user123", Correo = "test@test.com", Activo = true };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<UpdateResult>().Object);

        var request = new ForgotPasswordRequest("test@test.com");
        var result = await _service.ForgotPasswordAsync(request);

        result.Success.Should().BeTrue();
        result.RequestId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ForgotPasswordAsync_UsuarioNoExiste_RetornaFalse()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);

        var request = new ForgotPasswordRequest("noexist@test.com");
        var result = await _service.ForgotPasswordAsync(request);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ResetPasswordAsync_TokenValido_RetornaTrue()
    {
        var user = new UsuarioWeb
        {
            Id = "user123", ResetPasswordToken = "valid-token",
            ResetPasswordExpira = DateTime.UtcNow.AddHours(1)
        };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<UpdateResult>().Object);

        var request = new ResetPasswordRequest("valid-token", "NewPassword123!");
        var result = await _service.ResetPasswordAsync(request);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_TokenInvalido_RetornaFalse()
    {
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync((UsuarioWeb?)null);

        var request = new ResetPasswordRequest("invalid-token", "NewPassword123!");
        var result = await _service.ResetPasswordAsync(request);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CambiarPasswordAsync_PasswordCorrecta_RetornaTrue()
    {
        var user = new UsuarioWeb
        {
            Id = "user123", PasswordHash = PasswordHasher.Hash("OldPass123!")
        };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);
        _mockUsuarios.Setup(c => c.UpdateOneAsync(
            It.IsAny<FilterDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateDefinition<UsuarioWeb>>(),
            It.IsAny<UpdateOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<UpdateResult>().Object);

        var request = new CambiarPasswordRequest("OldPass123!", "NewPass123!");
        var result = await _service.CambiarPasswordAsync("user123", request);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CambiarPasswordAsync_PasswordIncorrecta_RetornaFalse()
    {
        var user = new UsuarioWeb
        {
            Id = "user123", PasswordHash = PasswordHasher.Hash("CorrectPass123!")
        };
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);

        var request = new CambiarPasswordRequest("WrongPass123!", "NewPass123!");
        var result = await _service.CambiarPasswordAsync("user123", request);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RevocarTodasLasSesionesAsync_UsuarioExistente_RetornaTrue()
    {
        var mockUpdateResult = new Mock<UpdateResult>();
        mockUpdateResult.Setup(r => r.ModifiedCount).Returns(3);
        _mockRefreshTokens.Setup(c => c.UpdateManyAsync(
                It.IsAny<FilterDefinition<RefreshToken>>(),
                It.IsAny<UpdateDefinition<RefreshToken>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpdateResult.Object);

        var result = await _service.RevocarTodasLasSesionesAsync("user123");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshTokenAsync_Paciente_RetornaRefreshTokenResponse()
    {
        var storedToken = new RefreshToken
        {
            Id = "rt1", UsuarioId = "pac123", Rol = "paciente", Token = "old-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedAt = DateTime.UtcNow
        };
        var paciente = new Paciente { Id = "pac123", CodigoAccesoQr = "ABC123", Nombre = "Paciente Test" };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<RefreshToken>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>()))
            .ReturnsAsync(storedToken);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Paciente>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Paciente, bool>>>()))
            .ReturnsAsync(paciente);

        var mockUpdateResult = new Mock<UpdateResult>();
        mockUpdateResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockRefreshTokens.Setup(c => c.UpdateManyAsync(
                It.IsAny<FilterDefinition<RefreshToken>>(),
                It.IsAny<UpdateDefinition<RefreshToken>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpdateResult.Object);

        var request = new RefreshTokenRequest("old-refresh-token");
        var result = await _service.RefreshTokenAsync(request);

        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshTokenAsync_Cuidador_RetornaRefreshTokenResponse()
    {
        var storedToken = new RefreshToken
        {
            Id = "rt2", UsuarioId = "cuid123", Rol = "cuidador", Token = "old-refresh-token-cuid",
            ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedAt = DateTime.UtcNow
        };
        var cuidador = new Cuidador { Id = "cuid123", CodigoAccesoQr = "CU-ABC", Nombre = "Cuidador Test" };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<RefreshToken>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>()))
            .ReturnsAsync(storedToken);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Cuidador>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Cuidador, bool>>>()))
            .ReturnsAsync(cuidador);

        var mockUpdateResult = new Mock<UpdateResult>();
        mockUpdateResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockRefreshTokens.Setup(c => c.UpdateManyAsync(
                It.IsAny<FilterDefinition<RefreshToken>>(),
                It.IsAny<UpdateDefinition<RefreshToken>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpdateResult.Object);

        var request = new RefreshTokenRequest("old-refresh-token-cuid");
        var result = await _service.RefreshTokenAsync(request);

        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshTokenAsync_Dueno_RetornaRefreshTokenResponse()
    {
        var storedToken = new RefreshToken
        {
            Id = "rt3", UsuarioId = "user123", Rol = "dueno", Token = "old-refresh-token-dueno",
            ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedAt = DateTime.UtcNow
        };
        var user = new UsuarioWeb
        {
            Id = "user123", Correo = "test@test.com", Nombre = "Test", ApellidoPaterno = "User"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<RefreshToken>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>()))
            .ReturnsAsync(storedToken);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<Plan, bool>>>()))
            .ReturnsAsync((Plan?)null);

        var mockUpdateResult = new Mock<UpdateResult>();
        mockUpdateResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockRefreshTokens.Setup(c => c.UpdateManyAsync(
                It.IsAny<FilterDefinition<RefreshToken>>(),
                It.IsAny<UpdateDefinition<RefreshToken>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpdateResult.Object);

        var request = new RefreshTokenRequest("old-refresh-token-dueno");
        var result = await _service.RefreshTokenAsync(request);

        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshTokenAsync_WebEsAdmin_EmiteRolAdmin()
    {
        var storedToken = new RefreshToken
        {
            Id = "rt4", UsuarioId = "admin123", Rol = "dueno", Token = "old-refresh-token-admin",
            ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedAt = DateTime.UtcNow
        };
        var user = new UsuarioWeb
        {
            Id = "admin123", Correo = "admin@test.com", EsAdmin = true,
            Nombre = "Admin", ApellidoPaterno = "Sistema"
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<RefreshToken>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>()))
            .ReturnsAsync(storedToken);
        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<UsuarioWeb>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<UsuarioWeb, bool>>>()))
            .ReturnsAsync(user);

        var mockUpdateResult = new Mock<UpdateResult>();
        mockUpdateResult.Setup(r => r.ModifiedCount).Returns(1);
        _mockRefreshTokens.Setup(c => c.UpdateManyAsync(
                It.IsAny<FilterDefinition<RefreshToken>>(),
                It.IsAny<UpdateDefinition<RefreshToken>>(),
                It.IsAny<UpdateOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUpdateResult.Object);

        var request = new RefreshTokenRequest("old-refresh-token-admin");
        var result = await _service.RefreshTokenAsync(request);

        result.Should().NotBeNull();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result!.AccessToken);
        jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value.Should().Be("admin");
    }

    [Fact]
    public async Task RefreshTokenAsync_TokenInactivo_RetornaNull()
    {
        var storedToken = new RefreshToken
        {
            Id = "rt4", UsuarioId = "user123", Rol = "dueno", Token = "expired-token",
            ExpiresAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow
        };

        _mockDb.Setup(db => db.FindFirstOrDefaultAsync(
                It.IsAny<IMongoCollection<RefreshToken>>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<RefreshToken, bool>>>()))
            .ReturnsAsync(storedToken);

        var request = new RefreshTokenRequest("expired-token");
        var result = await _service.RefreshTokenAsync(request);

        result.Should().BeNull();
    }
}
