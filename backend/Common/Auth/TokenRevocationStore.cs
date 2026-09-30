using StackExchange.Redis;

namespace backend.Common.Auth;

/// <summary>
/// Access tokens revoked by sign-out, keyed by their <c>jti</c>, in the
/// security Redis instance (<c>ConnectionStrings:Redis</c>, noeviction — an
/// evicted entry here would silently bring a signed-out token back to life).
///
/// Each entry lives exactly as long as the token it revokes would have, so the
/// set never grows past the tokens that are still valid. Signing out ends that
/// one session only; deactivating an account or resetting its password still
/// ends all of them, through <see cref="AdminSessionValidator"/>.
///
/// Registered only when Redis is configured. Production always has it (compose
/// requires it, and rate limiting refuses to start without it); the integration
/// suite runs without Redis unless a test provides one.
/// </summary>
public sealed class TokenRevocationStore(IConnectionMultiplexer redis)
{
    private static RedisKey KeyFor(string tokenId) => $"subul:revoked-token:{tokenId}";

    public async Task RevokeAsync(string tokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        // `exp` is a UTC instant by definition, so this compares UTC with UTC;
        // the DateTime.Now rule is about timestamps stored in Postgres.
        var remaining = expiresAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return;

        await redis.GetDatabase()
            .StringSetAsync(KeyFor(tokenId), "1", remaining)
            .WaitAsync(cancellationToken);
    }

    public async Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken) =>
        await redis.GetDatabase()
            .KeyExistsAsync(KeyFor(tokenId))
            .WaitAsync(cancellationToken);
}
