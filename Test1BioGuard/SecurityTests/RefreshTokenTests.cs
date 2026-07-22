using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BioGuard.Api.Services;

namespace Test1BioGuard.SecurityTests;

public class RefreshTokenTests
{
    private readonly AuthService _service;

    public RefreshTokenTests()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "BioGuard2024SecretKeyForJWTAuthentication!@#$%^&*()");

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "BioGuard2024SecretKeyForJWTAuthentication!@#$%^&*()",
                ["Jwt:Issuer"] = "BioGuardApi",
                ["Jwt:Audience"] = "BioGuardApp",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Jwt:RefreshTokenDays"] = "7"
            }).Build();

        var mockDb = new Moq.Mock<BioGuard.Api.Config.IMongoDbContext>();
        var mockLogger = new Moq.Mock<ILogger<AuthService>>();
        _service = new AuthService(mockDb.Object, config, new HttpClient(), mockLogger.Object);
    }

    [Fact]
    public void GenerateRefreshToken_RetornaTokenNoVacio()
    {
        var token = _service.GenerateRefreshToken();

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateRefreshToken_EsBase64()
    {
        var token = _service.GenerateRefreshToken();

        var act = () => Convert.FromBase64String(token);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateRefreshToken_LongitudSuficiente()
    {
        var token = _service.GenerateRefreshToken();

        var bytes = Convert.FromBase64String(token);
        bytes.Length.Should().BeGreaterThanOrEqualTo(64);
    }

    [Fact]
    public void GenerateRefreshToken_CadaLlamadaEsUnica()
    {
        var token1 = _service.GenerateRefreshToken();
        var token2 = _service.GenerateRefreshToken();

        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GenerateRefreshToken_CryptoRandom()
    {
        var tokens = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            tokens.Add(_service.GenerateRefreshToken());
        }

        tokens.Count.Should().Be(100);
    }

    [Fact]
    public void GenerateRefreshToken_LongitudConsistente()
    {
        var lengths = new List<int>();
        for (int i = 0; i < 10; i++)
        {
            var token = _service.GenerateRefreshToken();
            lengths.Add(token.Length);
        }

        lengths.Distinct().Count().Should().Be(1);
    }
}
