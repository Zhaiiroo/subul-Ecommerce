using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Common.Auth;
using backend.Domain.Entities;
using backend.Infrastructure.Persistence;
using backend.Tests.Infrastructure;

namespace backend.Tests.Features.AdminUserFeature;

/// <summary>
/// Covers the parts that only exist once the whole pipeline is in play: the
/// superadmin-only policy, the must-change lockout, and the fact that a
/// deactivation or a reset retires a token that is already in someone's hands.
/// </summary>
[Collection("Database")]
public class AdminUserManagementIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AdminUserManagementIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _factory = new TestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}@test.com";

    private async Task<AdminUser> SeedAsync(
        string email,
        string role,
        string password,
        bool mustChangePassword = false)
    {
        await using var context = _fixture.CreateContext();
        var user = new AdminUser
        {
            Name = $"Integration {role}",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
            IsActive = true,
            MustChangePassword = mustChangePassword,
            PasswordChangedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        };
        context.AdminUsers.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<HttpClient> SignInAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var accessToken = body.GetProperty("data").GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    [Fact]
    public async Task GET_AdminUsers_AsStaff_Returns403()
    {
        const string password = "StaffPass123!";
        var email = UniqueEmail("policy-staff");
        await SeedAsync(email, AdminUserRoles.Staff, password);

        using var staffClient = await SignInAsync(email, password);
        var response = await staffClient.GetAsync("/api/admin-users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_AdminUsers_AsSuperAdmin_Returns200()
    {
        const string password = "SuperPass123!";
        var email = UniqueEmail("policy-super");
        await SeedAsync(email, AdminUserRoles.SuperAdmin, password);

        using var superClient = await SignInAsync(email, password);
        var response = await superClient.GetAsync("/api/admin-users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GET_AdminUsers_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/admin-users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_AdminUsers_ResponseNeverCarriesPasswordHash()
    {
        const string password = "SuperPass123!";
        var actorEmail = UniqueEmail("create-actor");
        await SeedAsync(actorEmail, AdminUserRoles.SuperAdmin, password);

        using var superClient = await SignInAsync(actorEmail, password);
        var response = await superClient.PostAsJsonAsync("/api/admin-users", new
        {
            name = "Created Over HTTP",
            email = UniqueEmail("created-over-http"),
            role = AdminUserRoles.Manager,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("temporaryPassword", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MustChangePassword_BlocksEverythingButTheChangeRoute()
    {
        const string password = "TempPass123!";
        var email = UniqueEmail("must-change");
        await SeedAsync(email, AdminUserRoles.SuperAdmin, password, mustChangePassword: true);

        using var client = await SignInAsync(email, password);

        var blocked = await client.GetAsync("/api/admin-users");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        var allowed = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        var changed = await client.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = password,
            newPassword = "ChosenPass456",
        });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        // The stamp moved, so the token that just made that call is spent.
        var afterChange = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterChange.StatusCode);

        using var reSignedIn = await SignInAsync(email, "ChosenPass456");
        var nowOpen = await reSignedIn.GetAsync("/api/admin-users");
        Assert.Equal(HttpStatusCode.OK, nowOpen.StatusCode);
    }

    [Fact]
    public async Task Deactivation_InvalidatesAnAlreadyIssuedToken()
    {
        const string actorPassword = "SuperPass123!";
        const string targetPassword = "TargetPass123!";
        var actorEmail = UniqueEmail("deactivate-actor");
        var targetEmail = UniqueEmail("deactivate-target");

        await SeedAsync(actorEmail, AdminUserRoles.SuperAdmin, actorPassword);
        var target = await SeedAsync(targetEmail, AdminUserRoles.Manager, targetPassword);

        using var targetClient = await SignInAsync(targetEmail, targetPassword);
        Assert.Equal(HttpStatusCode.OK, (await targetClient.GetAsync("/api/auth/me")).StatusCode);

        using var superClient = await SignInAsync(actorEmail, actorPassword);
        var statusResponse = await superClient.PutAsJsonAsync(
            $"/api/admin-users/{target.Id}/status",
            new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var afterDeactivation = await targetClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterDeactivation.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_InvalidatesTheTargetsSessionAndIssuesAUsablePassword()
    {
        const string actorPassword = "SuperPass123!";
        const string targetPassword = "TargetPass123!";
        var actorEmail = UniqueEmail("reset-actor");
        var targetEmail = UniqueEmail("reset-target");

        await SeedAsync(actorEmail, AdminUserRoles.SuperAdmin, actorPassword);
        var target = await SeedAsync(targetEmail, AdminUserRoles.Manager, targetPassword);

        using var targetClient = await SignInAsync(targetEmail, targetPassword);
        using var superClient = await SignInAsync(actorEmail, actorPassword);

        var resetResponse = await superClient.PostAsJsonAsync(
            $"/api/admin-users/{target.Id}/reset-password",
            new { newPassword = (string?)null });
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var resetBody = await resetResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var temporaryPassword = resetBody
            .GetProperty("data")
            .GetProperty("temporaryPassword")
            .GetString();
        Assert.False(string.IsNullOrWhiteSpace(temporaryPassword));

        var afterReset = await targetClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterReset.StatusCode);

        // The temporary password works, and lands the account in the forced-change state.
        using var reSignedIn = await SignInAsync(targetEmail, temporaryPassword!);
        var gated = await reSignedIn.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.Forbidden, gated.StatusCode);
    }
}
