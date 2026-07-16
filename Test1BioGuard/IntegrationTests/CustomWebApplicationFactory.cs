using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using BioGuard.Api.Config;

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
