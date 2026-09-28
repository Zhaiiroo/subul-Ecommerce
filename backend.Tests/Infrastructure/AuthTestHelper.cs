using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Domain.Entities;
using backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Infrastructure;

public static class AuthTestHelper
{
    public const string DefaultAdminEmail = "admin@subul.iq";
    public const string DefaultAdminPassword = DbSeeder.SeedAdminPassword;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task SeedAdminUserAsync(AppDbContext context)
    {
        // Keyed on the default account rather than "any admin user": the
        // admin-user management tests create accounts of their own, and an
        // Any() check would then decide this one already exists and skip it.
        if (await context.AdminUsers.AnyAsync(u => u.Email == DefaultAdminEmail))
            return;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(DefaultAdminPassword);
        context.AdminUsers.Add(new AdminUser
        {
            Name = "Test Admin",
            Email = DefaultAdminEmail,
            PasswordHash = passwordHash,
            Role = "superadmin",
            IsActive = true,
            PasswordChangedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        });
        await context.SaveChangesAsync();
    }

    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        TestWebApplicationFactory factory,
        AppDbContext context,
        string email = DefaultAdminEmail,
        string password = DefaultAdminPassword)
    {
        await SeedAdminUserAsync(context);

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var accessToken = body.GetProperty("data").GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Login response missing accessToken");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
