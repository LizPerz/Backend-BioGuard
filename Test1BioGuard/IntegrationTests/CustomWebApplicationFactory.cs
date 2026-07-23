using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace Test1BioGuard.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<BioGuard.Api.Program>
{
    public Mock<IMongoDbContext> MockDbContext { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("MONGODB_CONNECTION_STRING", "mongodb://localhost:27017");
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "BioGuard2024SecretKeyForJWTAuthentication!@#$%^&*()");

        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IMongoDbContext));
            if (descriptor != null) services.Remove(descriptor);

            var configDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(MongoDbConfig));
            if (configDescriptor != null) services.Remove(configDescriptor);

            MockDbContext.Setup(db => db.Planes).Returns(new Mock<IMongoCollection<Plan>>().Object);
            MockDbContext.Setup(db => db.UsuariosWeb).Returns(new Mock<IMongoCollection<UsuarioWeb>>().Object);
            MockDbContext.Setup(db => db.Pacientes).Returns(new Mock<IMongoCollection<Paciente>>().Object);
            MockDbContext.Setup(db => db.Cuidadores).Returns(new Mock<IMongoCollection<Cuidador>>().Object);
            MockDbContext.Setup(db => db.Dispositivos).Returns(new Mock<IMongoCollection<Dispositivo>>().Object);
            MockDbContext.Setup(db => db.LecturasSensores).Returns(new Mock<IMongoCollection<LecturaSensor>>().Object);
            MockDbContext.Setup(db => db.EventosMetabolicos).Returns(new Mock<IMongoCollection<EventoMetabolico>>().Object);
            MockDbContext.Setup(db => db.TrackingGps).Returns(new Mock<IMongoCollection<TrackingGps>>().Object);
            MockDbContext.Setup(db => db.Notificaciones).Returns(new Mock<IMongoCollection<Notificacion>>().Object);
            MockDbContext.Setup(db => db.Auditoria).Returns(new Mock<IMongoCollection<Auditoria>>().Object);
            MockDbContext.Setup(db => db.Pagos).Returns(new Mock<IMongoCollection<Pago>>().Object);
            MockDbContext.Setup(db => db.PrediccionesMl).Returns(new Mock<IMongoCollection<PrediccionMl>>().Object);
            MockDbContext.Setup(db => db.ModelosMl).Returns(new Mock<IMongoCollection<ModeloMl>>().Object);
            MockDbContext.Setup(db => db.FcmTokens).Returns(new Mock<IMongoCollection<FcmToken>>().Object);
            MockDbContext.Setup(db => db.RefreshTokens).Returns(new Mock<IMongoCollection<RefreshToken>>().Object);
            MockDbContext.Setup(db => db.Medicamentos).Returns(new Mock<IMongoCollection<Medicamento>>().Object);
            MockDbContext.Setup(db => db.Alertas).Returns(new Mock<IMongoCollection<Alerta>>().Object);
            MockDbContext.Setup(db => db.TokenBlacklist).Returns(new Mock<IMongoCollection<TokenBlacklist>>().Object);

            services.AddSingleton(MockDbContext.Object);
            services.AddSingleton(new MongoDbConfig
            {
                ConnectionString = "mongodb://localhost:27017",
                DatabaseName = "bioguard_test"
            });

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
            });
        });
    }
}
