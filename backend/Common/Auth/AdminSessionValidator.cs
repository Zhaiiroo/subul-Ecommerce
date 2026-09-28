using System.Security.Claims;
using backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

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
}
