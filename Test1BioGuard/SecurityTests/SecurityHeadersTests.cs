using System.Net;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;

namespace Test1BioGuard.SecurityTests;

public class SecurityHeadersTests : IClassFixture<IntegrationTests.CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;

    public SecurityHeadersTests(IntegrationTests.CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockDb = factory.MockDbContext;

        var mockPlanes = new Mock<IMongoCollection<Plan>>();
        _mockDb.Setup(db => db.Planes).Returns(mockPlanes.Object);
        _mockDb.Setup(db => db.FindToListAsync(
                It.IsAny<IMongoCollection<Plan>>(),
                It.IsAny<FilterDefinition<Plan>>(),
                It.IsAny<SortDefinition<Plan>?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()))
            .ReturnsAsync(new List<Plan>());
    }

    [Fact]
    public async Task Headers_XContentTypeOptions_EsNosniff()
    {
        var response = await _client.GetAsync("/api/Planes");
        var headerValues = response.Headers.Contains("X-Content-Type-Options")
            ? string.Join(",", response.Headers.GetValues("X-Content-Type-Options"))
            : string.Join(",", response.Content.Headers.Contains("X-Content-Type-Options")
                ? response.Content.Headers.GetValues("X-Content-Type-Options") : Array.Empty<string>());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        headerValues.Should().Be("nosniff");
    }

    [Fact]
    public async Task Headers_XFrameOptions_EsDeny()
    {
        var response = await _client.GetAsync("/api/Planes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var headers = response.Headers.ToString();
        headers.Should().Contain("X-Frame-Options");
    }

    [Fact]
    public async Task Headers_XXSSProtection_EsActivo()
    {
        var response = await _client.GetAsync("/api/Planes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var headers = response.Headers.ToString();
        headers.Should().Contain("X-XSS-Protection");
    }

    [Fact]
    public async Task Headers_ReferrerPolicy_EsStrictOrigin()
    {
        var response = await _client.GetAsync("/api/Planes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var headers = response.Headers.ToString();
        headers.Should().Contain("Referrer-Policy");
    }

    [Fact]
    public async Task Headers_PermissionsPolicy_BloqueaCamara()
    {
        var response = await _client.GetAsync("/api/Planes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var headers = response.Headers.ToString();
        headers.Should().Contain("Permissions-Policy");
    }

    [Fact]
    public async Task Headers_HSTS_EsConfigurado()
    {
        var response = await _client.GetAsync("/api/Planes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var headers = response.Headers.ToString();
        headers.Should().Contain("Strict-Transport-Security");
    }

    [Fact]
    public async Task Headers_NoExponeXPoweredBy()
    {
        var response = await _client.GetAsync("/api/Planes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("X-Powered-By").Should().BeFalse();
    }
}
