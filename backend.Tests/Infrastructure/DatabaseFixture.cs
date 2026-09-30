using backend.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace backend.Tests.Infrastructure;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("testdb")
        .WithUsername("test")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5432))
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        await _container.StartAsync();

        // MigrateAsync, not EnsureCreatedAsync: EnsureCreated builds the schema
        // straight from the model and would pass even when the migrations that
        // production actually runs are missing a column. Migrating here means
        // every test in the suite runs against the deployed schema.
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Creates an empty database in the same container and returns its
    /// connection string, for tests that need to drive migrations themselves.
    /// </summary>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = "db_" + Guid.NewGuid().ToString("N");

        await using (var context = CreateContext())
        {
            // The name is generated above, never caller-supplied.
#pragma warning disable EF1002
            await context.Database.ExecuteSqlRawAsync($"CREATE DATABASE \"{name}\"");
#pragma warning restore EF1002
        }

        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    public static AppDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public AppDbContext CreateContext() => CreateContext(ConnectionString);

    public async Task ResetAsync()
    {
        await using var context = CreateContext();
        context.Categories.RemoveRange(context.Categories);
        await context.SaveChangesAsync();
    }
}
