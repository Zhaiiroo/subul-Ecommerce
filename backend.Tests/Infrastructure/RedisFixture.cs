using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace backend.Tests.Infrastructure;

/// <summary>
/// A real Redis for the tests whose behaviour lives in Redis — token revocation
/// and the rate-limit budgets. The rest of the suite runs without one
/// (appsettings.Testing.json disables rate limiting), so this is opt-in per
/// test class through <c>IClassFixture&lt;RedisFixture&gt;</c>.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    /// <summary>A port nothing listens on, so every Redis call fails fast.</summary>
    public const string Unreachable =
        "127.0.0.1:6399,abortConnect=false,connectTimeout=250,syncTimeout=250";

    private readonly RedisContainer _container = new RedisBuilder("redis:8.10.1-alpine").Build();
    private ConnectionMultiplexer? _admin;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _admin = await ConnectionMultiplexer.ConnectAsync($"{ConnectionString},allowAdmin=true");
    }

    public IDatabase Database => _admin!.GetDatabase();

    /// <summary>Empties Redis so a test starts with full budgets and no revocations.</summary>
    public async Task FlushAsync()
    {
        foreach (var endpoint in _admin!.GetEndPoints())
            await _admin.GetServer(endpoint).FlushAllDatabasesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_admin is not null)
            await _admin.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>The API with rate limiting and revocation backed by <paramref name="redis"/>.</summary>
    public static WebApplicationFactory<Program> WithRedis(TestWebApplicationFactory factory, string redis) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RedisRateLimit:Enabled", "true");
            builder.UseSetting("ConnectionStrings:Redis", redis);
        });
}
