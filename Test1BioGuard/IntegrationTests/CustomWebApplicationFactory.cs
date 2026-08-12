using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using BioGuard.Api.Services;

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
            var mockUsuariosWeb = new Mock<IMongoCollection<UsuarioWeb>>();
            mockUsuariosWeb.Setup(c => c.UpdateOneAsync(
                    It.IsAny<FilterDefinition<UsuarioWeb>>(),
                    It.IsAny<UpdateDefinition<UsuarioWeb>>(),
                    It.IsAny<UpdateOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<UpdateResult>().Object);
            MockDbContext.Setup(db => db.UsuariosWeb).Returns(mockUsuariosWeb.Object);
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
            MockDbContext.Setup(db => db.FcmTokens).Returns(new Mock<IMongoCollection<FcmToken>>().Object);
            MockDbContext.Setup(db => db.RefreshTokens).Returns(new Mock<IMongoCollection<RefreshToken>>().Object);
            MockDbContext.Setup(db => db.Medicamentos).Returns(new Mock<IMongoCollection<Medicamento>>().Object);
            MockDbContext.Setup(db => db.Alertas).Returns(new Mock<IMongoCollection<Alerta>>().Object);
            MockDbContext.Setup(db => db.TokenBlacklist).Returns(new Mock<IMongoCollection<TokenBlacklist>>().Object);
            MockDbContext.Setup(db => db.DeviceSessions).Returns(new Mock<IMongoCollection<DeviceSession>>().Object);
            MockDbContext.Setup(db => db.ReportesCompartidos).Returns(new Mock<IMongoCollection<ReporteCompartido>>().Object);
            MockDbContext.Setup(db => db.TicketsSoporte).Returns(new Mock<IMongoCollection<TicketSoporte>>().Object);

            services.AddSingleton(MockDbContext.Object);
            services.AddSingleton(new MongoDbConfig
            {
                ConnectionString = "mongodb://localhost:27017",
                DatabaseName = "bioguard_test"
            });

            var mockEmailService = new Mock<IEmailService>();
            mockEmailService.Setup(s => s.SendVerificationCodeAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            mockEmailService.Setup(s => s.SendPasswordResetAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            services.AddSingleton(mockEmailService.Object);

            // Replace real payment gateways with mocks for integration tests
            var stripeDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(StripePaymentGateway));
            if (stripeDescriptor != null) services.Remove(stripeDescriptor);
            var factoryDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(PaymentGatewayFactory));
            if (factoryDescriptor != null) services.Remove(factoryDescriptor);

            var mockStripeOptions = new Mock<IOptions<StripeOptions>>();
            mockStripeOptions.Setup(o => o.Value).Returns(new StripeOptions());
            var mockStripeLogger = new Mock<ILogger<StripePaymentGateway>>();
            services.AddSingleton(new StripePaymentGateway(mockStripeOptions.Object, mockStripeLogger.Object));

            services.AddSingleton(sp =>
            {
                var stripe = sp.GetRequiredService<StripePaymentGateway>();
                return new PaymentGatewayFactory(stripe);
            });

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
            });
        });
    }
}
