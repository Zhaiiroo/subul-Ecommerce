using backend.Common.Auth;
using backend.Domain.Entities;
using backend.Features.AdminUserFeature.GetCurrentAdminUser;
using backend.Features.AdminUserFeature.LoginAdminUser;
using backend.Features.AdminUserFeature.LogoutAdminUser;
using backend.Infrastructure.Persistence;
using backend.Tests.Infrastructure;

namespace backend.Tests.Features.AdminUserFeature;

[Collection("Database")]
public class AdminUserHandlerTests(DatabaseFixture fixture)
{
    private static JwtTokenService CreateJwtTokenService()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Secret = "test-jwt-secret-at-least-32-characters-long",
            Issuer = "subul-admin",
            Audience = "subul-admin-panel",
            AccessTokenMinutes = 480,
        });
        return new JwtTokenService(options);
    }

    private static async Task<AdminUser> SeedAdminUserAsync(
        AppDbContext context,
        string email,
        string password,
        bool isActive = true)
    {
        var user = new AdminUser
        {
            Name = "Test Admin",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "superadmin",
            IsActive = isActive,
            CreatedAt = DateTime.Now,
        };
        context.AdminUsers.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task LoginAdminUser_ValidCredentials_ReturnsAccessToken()
    {
        await using var context = fixture.CreateContext();
        const string email = "login-success@test.com";
        const string password = "ValidPass123!";
        await SeedAdminUserAsync(context, email, password);

        var handler = new LoginAdminUserHandler(context, CreateJwtTokenService());
        var result = await handler.Handle(
            new LoginAdminUserCommand(email, password),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.AccessToken));
        Assert.Equal(email, result.Value.User.Email);
    }

    [Fact]
    public async Task LoginAdminUser_WrongPassword_ReturnsUnauthorized()
    {
        await using var context = fixture.CreateContext();
        const string email = "login-wrong-pass@test.com";
        await SeedAdminUserAsync(context, email, "CorrectPass123!");

        var handler = new LoginAdminUserHandler(context, CreateJwtTokenService());
        var result = await handler.Handle(
            new LoginAdminUserCommand(email, "WrongPass123!"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("unauthorized", result.Error);
    }

    [Fact]
    public async Task LoginAdminUser_InactiveUser_ReturnsUnauthorized()
    {
        await using var context = fixture.CreateContext();
        const string email = "login-inactive@test.com";
        const string password = "ValidPass123!";
        await SeedAdminUserAsync(context, email, password, isActive: false);

        var handler = new LoginAdminUserHandler(context, CreateJwtTokenService());
        var result = await handler.Handle(
            new LoginAdminUserCommand(email, password),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("unauthorized", result.Error);
    }

    [Fact]
    public async Task LoginAdminUser_UnknownEmail_ReturnsUnauthorized()
    {
        await using var context = fixture.CreateContext();
        var handler = new LoginAdminUserHandler(context, CreateJwtTokenService());
        var result = await handler.Handle(
            new LoginAdminUserCommand("unknown@test.com", "AnyPass123!"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("unauthorized", result.Error);
    }

    [Fact]
    public async Task LoginAdminUser_InvalidPasswordHash_ReturnsUnauthorized()
    {
        await using var context = fixture.CreateContext();
        const string email = "login-bad-hash@test.com";
        context.AdminUsers.Add(new AdminUser
        {
            Name = "Bad Hash Admin",
            Email = email,
            PasswordHash = "not-a-valid-bcrypt-hash!!!!!!",
            Role = "superadmin",
            IsActive = true,
            CreatedAt = DateTime.Now,
        });
        await context.SaveChangesAsync();

        var handler = new LoginAdminUserHandler(context, CreateJwtTokenService());
        var result = await handler.Handle(
            new LoginAdminUserCommand(email, "Admin123!"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("unauthorized", result.Error);
    }

    [Fact]
    public async Task LogoutAdminUser_WithoutRevocationStore_ReturnsSuccess()
    {
        // No Redis configured: nothing to revoke into, the token simply expires.
        // Revocation itself is covered against a real Redis in SessionRevocationTests.
        var handler = new LogoutAdminUserHandler();
        var result = await handler.Handle(
            new LogoutAdminUserCommand(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow.AddHours(1)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Success);
    }

    [Fact]
    public async Task GetCurrentAdminUser_ValidId_ReturnsUser()
    {
        await using var context = fixture.CreateContext();
        var user = await SeedAdminUserAsync(context, "me-success@test.com", "ValidPass123!");

        var handler = new GetCurrentAdminUserHandler(context);
        var result = await handler.Handle(new GetCurrentAdminUserQuery(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Email, result.Value!.User.Email);
    }

    [Fact]
    public async Task GetCurrentAdminUser_NotFound_ReturnsFailure()
    {
        await using var context = fixture.CreateContext();
        var handler = new GetCurrentAdminUserHandler(context);
        var result = await handler.Handle(new GetCurrentAdminUserQuery(999_999), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
