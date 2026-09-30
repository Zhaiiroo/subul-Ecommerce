using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace backend.Common.Auth;

/// <summary>
/// Runs from <see cref="JwtBearerEvents.OnTokenValidated"/>, after the signature
/// and lifetime checks have passed.
///
/// A bearer token is a signed snapshot: on its own, disabling an account or
/// resetting its password changes nothing until the token expires, which here is
/// eight hours. Both are exactly the actions the admin-user screen exists to
/// perform, so the snapshot is reconciled with the row on every authenticated
/// request — one primary-key read, and only for callers that sent a token, which
/// means the anonymous storefront routes never pay for it.
/// </summary>
public static class AdminSessionValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal is null)
        {
            context.Fail("Token carries no principal.");
            return;
        }

        if (!long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var adminUserId))
        {
            context.Fail("Token carries no admin user id.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        var account = await db.AdminUsers
            .AsNoTracking()
            .Where(u => u.Id == adminUserId)
            .Select(u => new
            {
                u.IsActive,
                u.Role,
                u.MustChangePassword,
                u.PasswordChangedAt,
                u.CreatedAt,
            })
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (account is null || !account.IsActive)
        {
            context.Fail("Admin account is disabled or no longer exists.");
            return;
        }

        var currentStamp = AdminPasswordPolicy.StampFor(account.PasswordChangedAt, account.CreatedAt);
        if (!string.Equals(principal.FindFirstValue(AdminSessionClaims.PasswordStamp), currentStamp, StringComparison.Ordinal))
        {
            context.Fail("Password changed after this token was issued.");
            return;
        }

        if (!await IsStillSignedInAsync(context, principal))
            return;

        // The role and the must-change flag are refreshed from the row rather than
        // trusted from the token, so a demotion or a reset applies to the request
        // in flight instead of to the next sign-in.
        var refreshedClaims = principal.Claims
            .Where(claim =>
                claim.Type != ClaimTypes.Role &&
                claim.Type != AdminSessionClaims.MustChangePassword)
            .ToList();

        refreshedClaims.Add(new Claim(ClaimTypes.Role, account.Role));
        refreshedClaims.Add(new Claim(
            AdminSessionClaims.MustChangePassword,
            account.MustChangePassword ? "true" : "false"));

        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(
            refreshedClaims,
            principal.Identity?.AuthenticationType,
            ClaimTypes.Name,
            ClaimTypes.Role));
    }

    /// <summary>
    /// Rejects a token whose session was ended by sign-out.
    ///
    /// Fails closed: if Redis cannot answer, the request is refused rather than
    /// served with a token that may have been revoked. Only authenticated admin
    /// traffic reaches this, so an outage locks the panel for its duration and
    /// leaves the anonymous storefront untouched — and sign-in is refused during
    /// the same outage anyway (the login rate limit fails closed too).
    /// </summary>
    private static async Task<bool> IsStillSignedInAsync(TokenValidatedContext context, ClaimsPrincipal principal)
    {
        var revocations = context.HttpContext.RequestServices.GetService<TokenRevocationStore>();
        if (revocations is null)
            return true;

        var tokenId = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (string.IsNullOrEmpty(tokenId))
        {
            // Issued before tokens carried an id, so it could never be revoked.
            context.Fail("Token carries no id.");
            return false;
        }

        try
        {
            if (!await revocations.IsRevokedAsync(tokenId, context.HttpContext.RequestAborted))
                return true;

            context.Fail("Token was revoked by sign-out.");
            return false;
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(AdminSessionValidator))
                .LogError(exception, "Token revocation status is unavailable; refusing the request");

            context.Fail("Token revocation status is unavailable.");
            return false;
        }
    }
}
