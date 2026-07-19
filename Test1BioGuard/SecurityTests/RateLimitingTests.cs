using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Moq;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;

namespace Test1BioGuard.SecurityTests;

public class RateLimitingTests : IClassFixture<IntegrationTests.CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<IMongoDbContext> _mockDb;

    public RateLimitingTests(IntegrationTests.CustomWebApplicationFactory factory)
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
    public async Task Endpoints_GetNoLimitado_PermiteMultiples()
    {
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 5; i++)
        {
            responses.Add(await _client.GetAsync("/api/Planes"));
        }

        responses.Should().OnlyContain(r => r.StatusCode != HttpStatusCode.TooManyRequests);
    }
}
