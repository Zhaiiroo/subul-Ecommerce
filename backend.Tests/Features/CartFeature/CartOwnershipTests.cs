using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Domain.Entities;
using backend.Tests.Infrastructure;

namespace backend.Tests.Features.CartFeature;

/// <summary>
/// Regression tests for C-4: cart takeover. Cart ownership is proven by the
/// X-Cart-Session header alone. A caller-supplied userId used to resolve another
/// user's cart and then rebind its SessionId to the caller — a permanent takeover
/// triggered by a plain GET.
/// </summary>
[Collection("Database")]
public class CartOwnershipTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CartOwnershipTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new TestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GET_Cart_WithForeignUserId_DoesNotReturnVictimsCart()
    {
        var (victimUserId, victimSessionId, victimCartId) = await SeedVictimCartAsync();
        var attackerSessionId = Guid.NewGuid().ToString("N");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/carts?userId={victimUserId}");
        request.Headers.Add("X-Cart-Session", attackerSessionId);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var returnedCartId = body.GetProperty("data").GetProperty("id").GetInt64();

        Assert.NotEqual(victimCartId, returnedCartId);

        // And the victim's cart must still belong to the victim's session.
        await using var context = _fixture.CreateContext();
        var victimCart = await context.Carts.FindAsync(victimCartId);
        Assert.NotNull(victimCart);
        Assert.Equal(victimSessionId, victimCart!.SessionId);
    }

    [Fact]
    public async Task DELETE_CartItem_FromAnotherSession_IsRejected()
    {
        var (_, _, victimCartId) = await SeedVictimCartAsync();

        long victimItemId;
        await using (var context = _fixture.CreateContext())
        {
            var product = new Product
            {
                NameEn = $"Victim product {Guid.NewGuid():N}",
                Slug = $"victim-product-{Guid.NewGuid():N}",
                Price = 1000m,
                Currency = "IQD",
                Status = "active",
                StockQuantity = 10,
                MinOrderQuantity = 1,
                CreatedAt = DateTime.Now,
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var item = new CartItem
            {
                CartId = victimCartId,
                ProductId = product.Id,
                Quantity = 1,
                UnitPrice = product.Price,
                CreatedAt = DateTime.Now,
            };
            context.CartItems.Add(item);
            await context.SaveChangesAsync();
            victimItemId = item.Id;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/carts/items/{victimItemId}");
        request.Headers.Add("X-Cart-Session", Guid.NewGuid().ToString("N"));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var verifyContext = _fixture.CreateContext();
        Assert.NotNull(await verifyContext.CartItems.FindAsync(victimItemId));
    }

    // M-8: MergeCart took the target userId from the request body, and the only
    // tokens that exist are admin tokens, so every caller was either refused or
    // an IDOR. It was removed until real customer accounts exist; the route must
    // stay gone rather than quietly come back.
    [Fact]
    public async Task POST_CartMerge_RouteNoLongerExists()
    {
        // Authenticated on purpose: the fallback policy answers 401 for any
        // anonymous request, matched or not, so only a signed-in caller can tell
        // "removed" (404) apart from "protected".
        await using var context = _fixture.CreateContext();
        using var admin = await AuthTestHelper.CreateAuthenticatedClientAsync(_factory, context);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/carts/merge");
        request.Headers.Add("X-Cart-Session", Guid.NewGuid().ToString("N"));
        request.Content = JsonContent.Create(new { userId = 1 });

        var response = await admin.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Guest carts themselves stay anonymous — the fix must not break the storefront.
    [Fact]
    public async Task GET_Cart_WithSessionOnly_StaysAnonymous()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/carts");
        request.Headers.Add("X-Cart-Session", Guid.NewGuid().ToString("N"));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<(long UserId, string SessionId, long CartId)> SeedVictimCartAsync()
    {
        await using var context = _fixture.CreateContext();

        var user = new User
        {
            Email = $"victim-{Guid.NewGuid():N}@example.test",
            Phone = $"0770{Random.Shared.Next(1000000, 9999999)}",
            FirstName = "Victim",
            IsActive = true,
            CreatedAt = DateTime.Now,
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var sessionId = Guid.NewGuid().ToString("N");
        var cart = new Cart
        {
            UserId = user.Id,
            SessionId = sessionId,
            ExpiresAt = DateTime.Now.AddDays(30),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };
        context.Carts.Add(cart);
        await context.SaveChangesAsync();

        return (user.Id, sessionId, cart.Id);
    }
}
