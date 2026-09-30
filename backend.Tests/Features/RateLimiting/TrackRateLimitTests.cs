using System.Net;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace backend.Tests.Features.RateLimiting;

/// <summary>
/// Regression tests for M-3. Guest tracking takes an order number and a phone —
/// the whole credential for someone else's order — and used to share the
/// general 120-per-minute budget, which made enumeration a matter of patience.
/// It now has its own budget (10 per 5 minutes in appsettings.json) and fails
/// closed like login.
/// </summary>
[Collection("Database")]
public class TrackRateLimitTests(DatabaseFixture fixture, RedisFixture redis)
    : IClassFixture<RedisFixture>, IAsyncLifetime
{
    private const string TrackUrl = "/api/orders/track?orderNumber=ORD-20260101-AAAAAAAA&phone=07700000000";

    private TestWebApplicationFactory _factory = null!;
    private WebApplicationFactory<Program> _app = null!;

    public async Task InitializeAsync()
    {
        await redis.FlushAsync();
        _factory = new TestWebApplicationFactory(fixture.ConnectionString);
        _app = RedisFixture.WithRedis(_factory, redis.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Track_BeyondItsBudget_Returns429()
    {
        using var client = _app.CreateClient();

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var allowed = await client.GetAsync(TrackUrl);
            Assert.Equal(HttpStatusCode.NotFound, allowed.StatusCode);
        }

        var refused = await client.GetAsync(TrackUrl);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.True(refused.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Track_ExhaustedBudget_DoesNotStarveTheRestOfTheStore()
    {
        using var client = _app.CreateClient();
        for (var attempt = 0; attempt < 11; attempt++)
            await client.GetAsync(TrackUrl);

        // A shopper who mistyped an order number a few times must still be able
        // to browse and check out.
        var catalog = await client.GetAsync("/api/products?limit=1");
        Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
    }

    [Fact]
    public async Task Track_WhenRedisIsUnavailable_FailsClosed()
    {
        using var outageFactory = new TestWebApplicationFactory(fixture.ConnectionString);
        await using var outage = RedisFixture.WithRedis(outageFactory, RedisFixture.Unreachable);
        using var client = outage.CreateClient();

        var response = await client.GetAsync(TrackUrl);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
