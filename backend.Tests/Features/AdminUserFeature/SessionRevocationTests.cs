using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using backend.Common.Auth;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace backend.Tests.Features.AdminUserFeature;

/// <summary>
/// Regression tests for H-2 / H-5: sign-out used to return success and change
/// nothing, leaving the token valid for its remaining eight hours. It now
/// revokes that token in Redis, and every authenticated request checks.
/// </summary>
[Collection("Database")]
public class SessionRevocationTests(DatabaseFixture fixture, RedisFixture redis)
    : IClassFixture<RedisFixture>, IAsyncLifetime
{
    // Must match appsettings.Testing.json: the forged token below has to pass
    // signature validation so that only the missing jti can reject it.
    private const string TestJwtSecret = "test-jwt-secret-at-least-32-characters-long";

    private TestWebApplicationFactory _factory = null!;
    private WebApplicationFactory<Program> _app = null!;

    public async Task InitializeAsync()
    {
        await redis.FlushAsync();
        _factory = new TestWebApplicationFactory(fixture.ConnectionString);
        _app = RedisFixture.WithRedis(_factory, redis.ConnectionString);

        await using var context = fixture.CreateContext();
        await AuthTestHelper.SeedAdminUserAsync(context);
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Logout_RevokesTheTokenItWasCalledWith()
    {
        using var client = await SignInAsync(_app);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        // The same token, replayed after sign-out — the stolen-token case.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_LeavesTheSameAccountsOtherSessionsSignedIn()
    {
        using var laptop = await SignInAsync(_app);
        using var phone = await SignInAsync(_app);

        await laptop.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Revocation_IsRememberedOnlyAsLongAsTheTokenLives()
    {
        using var client = await SignInAsync(_app);
        await client.PostAsync("/api/auth/logout", null);

        var jti = new JwtSecurityTokenHandler()
            .ReadJwtToken(client.DefaultRequestHeaders.Authorization!.Parameter)
            .Id;
        var ttl = await redis.Database.KeyTimeToLiveAsync($"subul:revoked-token:{jti}");

        // Testing issues 480-minute tokens: the entry must expire with the token
        // rather than accumulate for ever.
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromMinutes(470), TimeSpan.FromMinutes(480));
    }

    [Fact]
    public async Task TokenWithoutAnId_IsRejected()
    {
        await using var context = fixture.CreateContext();
        var admin = await context.AdminUsers.AsNoTracking()
            .SingleAsync(u => u.Email == AuthTestHelper.DefaultAdminEmail);

        // Correctly signed and otherwise complete — only the jti is missing, as
        // on every token issued before this change. Such a token could never be
        // revoked, so it must not be honoured.
        var token = new JwtSecurityToken(
            issuer: "subul-admin",
            audience: "subul-admin-panel",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new Claim(ClaimTypes.Role, admin.Role),
                new Claim(AdminSessionClaims.PasswordStamp, AdminPasswordPolicy.StampFor(admin)),
                new Claim(AdminSessionClaims.MustChangePassword, "false"),
            ],
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret)),
                SecurityAlgorithms.HmacSha256));

        using var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task RedisUnavailable_AdminRequestsFailClosed_StorefrontStillServes()
    {
        // Signed in while Redis is up; the same token then meets an API whose
        // Redis is down. Tokens are portable between the two hosts (same secret,
        // same database), so this is a Redis outage seen from a live session.
        using var signedIn = await SignInAsync(_app);
        var bearer = signedIn.DefaultRequestHeaders.Authorization;

        using var outageFactory = new TestWebApplicationFactory(fixture.ConnectionString);
        await using var outage = RedisFixture.WithRedis(outageFactory, RedisFixture.Unreachable);

        using var admin = outage.CreateClient();
        admin.DefaultRequestHeaders.Authorization = bearer;
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/auth/me")).StatusCode);

        using var shopper = outage.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await shopper.GetAsync("/api/products?limit=1")).StatusCode);
    }

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = AuthTestHelper.DefaultAdminEmail,
            password = AuthTestHelper.DefaultAdminPassword,
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("data").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
