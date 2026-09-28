using backend.Common.Auth;
using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace backend.Infrastructure.Persistence;

/// <summary>
/// Creates the first admin account on an empty database, in every environment —
/// the production counterpart to <see cref="DbSeeder"/>, which only runs in
/// Development. It creates exactly one account and only when <c>admin_users</c>
/// is empty, so it never resurrects an account an operator deleted and never
/// changes an existing password.
///
/// The account is a superadmin and always starts with
/// <see cref="AdminUser.MustChangePassword"/> set: the bootstrap password exists
/// to get one person through the login form once, after which the panel makes
/// them pick their own before anything else is reachable.
/// </summary>
public static class AdminBootstrapper
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AdminBootstrapOptions>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        if (!options.Enabled)
            return;

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.AdminUsers.AnyAsync())
            return;

        var email = options.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("AdminBootstrap:Email must be set.");

        var configuredPassword = options.Password?.Trim();
        var generated = string.IsNullOrEmpty(configuredPassword);
        var password = generated ? AdminPasswordPolicy.Generate() : configuredPassword!;

        var policyError = AdminPasswordPolicy.Validate(password);
        if (policyError is not null)
            throw new InvalidOperationException($"AdminBootstrap:Password is invalid — {policyError}.");

        var now = DateTime.Now;
        db.AdminUsers.Add(new AdminUser
        {
            Name = string.IsNullOrWhiteSpace(options.Name) ? "Administrator" : options.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = AdminUserRoles.SuperAdmin,
            IsActive = true,
            MustChangePassword = true,
            PasswordChangedAt = now,
            CreatedAt = now,
        });

        await db.SaveChangesAsync();

        if (generated)
        {
            logger.LogWarning(
                "Bootstrapped the first admin account {Email} with the generated password {Password}. " +
                "It is shown once and is not recoverable; sign in and set a new password now.",
                email,
                password);
        }
        else
        {
            logger.LogInformation(
                "Bootstrapped the first admin account {Email} from AdminBootstrap:Password. " +
                "A password change is required at first sign-in.",
                email);
        }
    }
}
