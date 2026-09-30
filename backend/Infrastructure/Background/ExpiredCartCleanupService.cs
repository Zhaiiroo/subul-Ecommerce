using backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Infrastructure.Background;

public sealed class CartCleanupOptions
{
    public const string SectionName = "CartCleanup";

    public bool Enabled { get; init; } = true;
    public int IntervalHours { get; init; } = 6;
}

/// <summary>
/// Deletes guest carts whose <c>expires_at</c> has passed. The column was written
/// on every cart and read nowhere, so abandoned carts accumulated for ever;
/// cart_items go with them through the ON DELETE CASCADE foreign key.
///
/// Runs once at startup and then every <see cref="CartCleanupOptions.IntervalHours"/>.
/// Deletes in batches so a large backlog never becomes one long lock on carts.
/// </summary>
public sealed class ExpiredCartCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<CartCleanupOptions> options,
    ILogger<ExpiredCartCleanupService> logger) : BackgroundService
{
    private const int BatchSize = 1000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromHours(Math.Max(1, options.Value.IntervalHours)));

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var deleted = await DeleteExpiredAsync(db, DateTime.Now, stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Deleted {Count} expired cart(s)", deleted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed pass is retried on the next tick; it must not take the
                // host down with it.
                logger.LogError(exception, "Expired cart cleanup failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public static async Task<int> DeleteExpiredAsync(
        AppDbContext db,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var total = 0;

        while (true)
        {
            var expiredIds = await db.Carts
                .Where(c => c.ExpiresAt != null && c.ExpiresAt < now)
                .OrderBy(c => c.Id)
                .Select(c => c.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (expiredIds.Count == 0)
                return total;

            total += await db.Carts
                .Where(c => expiredIds.Contains(c.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
