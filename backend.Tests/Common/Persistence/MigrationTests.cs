using backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace backend.Tests.Common.Persistence;

/// <summary>
/// Production builds its schema only from the migrations (the `migrate` service
/// runs `--migrate`), so these guard the two ways that can go wrong: a model
/// change with no migration behind it, and a migration that cannot run against
/// the databases that already exist.
/// </summary>
[Collection("Database")]
public class MigrationTests(DatabaseFixture fixture)
{
    private const string InitialMigration = "20260623231933_Initial";

    [Fact]
    public void Model_HasNoChangesMissingFromMigrations()
    {
        using var context = fixture.CreateContext();

        // Fails when an entity or AppDbContext.Partial.cs changes without a
        // matching `dotnet ef migrations add`. The fix is to add the migration,
        // not to adjust this test.
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Migrate_OnEmptyDatabase_AppliesEveryMigration()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await using var context = DatabaseFixture.CreateContext(connectionString);

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(
            context.Database.GetMigrations(),
            await context.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Migrate_Twice_IsANoOp()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await using var context = DatabaseFixture.CreateContext(connectionString);

        await context.Database.MigrateAsync();

        // Every deploy runs `migrate`, including deploys with no schema change.
        var exception = await Record.ExceptionAsync(() => context.Database.MigrateAsync());
        Assert.Null(exception);
    }

    /// <summary>
    /// Reproduces the Development and Staging databases as they exist today:
    /// history records only Initial, and the admin password columns were added
    /// by hand from the retired docs/sql script. AdminUserPasswordPolicy must
    /// succeed there rather than fail with "column already exists".
    /// </summary>
    [Fact]
    public async Task AdminUserPasswordPolicy_OnHandUpgradedDatabase_Succeeds()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await using var context = DatabaseFixture.CreateContext(connectionString);

        await context.GetService<IMigrator>().MigrateAsync(InitialMigration);
        await context.Database.ExecuteSqlRawAsync("""
            ALTER TABLE admin_users ADD COLUMN must_change_password boolean NOT NULL DEFAULT false;
            ALTER TABLE admin_users ADD COLUMN password_changed_at timestamp without time zone;
            """);

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task AdminUserPasswordPolicy_BackfillsStampForExistingAccounts()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await using var context = DatabaseFixture.CreateContext(connectionString);

        await context.GetService<IMigrator>().MigrateAsync(InitialMigration);
        var createdAt = new DateTime(2026, 1, 15, 10, 30, 0);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO admin_users (name, email, password_hash, created_at)
            VALUES ('Existing', {$"existing-{Guid.NewGuid():N}@test.local"}, 'hash', {createdAt})
            """);

        await context.Database.MigrateAsync();

        // A null stamp would reject every token the account is issued.
        var account = await context.AdminUsers.AsNoTracking().SingleAsync();
        Assert.Equal(createdAt, account.PasswordChangedAt);
        Assert.False(account.MustChangePassword);
    }
}
