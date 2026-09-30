using backend.Common.Carts;
using backend.Domain.Entities;
using backend.Features.CartFeature.AddCartItem;
using backend.Features.CartFeature.GetCart;
using backend.Features.ProductFeature.CreateProduct;
using backend.Infrastructure.Background;
using backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Features.CartFeature;

/// <summary>
/// Regression tests for M-13: any anonymous GET with a made-up X-Cart-Session
/// header used to insert a cart row, with no format check, and nothing ever
/// deleted carts past their expires_at.
/// </summary>
[Collection("Database")]
public class CartSessionHardeningTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task GetCart_ForAnUnknownSession_ReturnsAnEmptyCartWithoutCreatingOne()
    {
        var session = CartSessionId.New();
        await using var context = fixture.CreateContext();

        var result = await new GetCartHandler(context).Handle(new GetCartQuery(session), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(session, result.Value.SessionId);
        Assert.False(await context.Carts.AnyAsync(c => c.SessionId == session));
    }

    [Theory]
    [InlineData("attacker-chosen-session")]
    [InlineData("00000000-0000-0000-0000-00000000000g")]
    [InlineData("a-very-long-value-that-used-to-go-straight-into-the-carts-table-unchecked-000000000000")]
    public async Task MalformedSession_IsRefusedOnReadAndWrite(string session)
    {
        await using var context = fixture.CreateContext();
        var productId = await CreateProductAsync(context);

        var read = await new GetCartHandler(context).Handle(new GetCartQuery(session), CancellationToken.None);
        var write = await new AddCartItemHandler(context).Handle(
            new AddCartItemCommand(session, productId, Quantity: 1),
            CancellationToken.None);

        Assert.Equal(CartSessionId.InvalidMessage, read.Error);
        Assert.Equal(CartSessionId.InvalidMessage, write.Error);
    }

    [Fact]
    public async Task AddCartItem_WithoutASession_MintsOne()
    {
        await using var context = fixture.CreateContext();
        var productId = await CreateProductAsync(context);

        var result = await new AddCartItemHandler(context).Handle(
            new AddCartItemCommand(null, productId, Quantity: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(CartSessionId.TryNormalize(result.Value!.SessionId, out _));
    }

    [Fact]
    public async Task EveryWrite_MovesTheExpiryForward()
    {
        var session = CartSessionId.New();
        await using var context = fixture.CreateContext();
        var productId = await CreateProductAsync(context);
        await new AddCartItemHandler(context).Handle(new AddCartItemCommand(session, productId, Quantity: 1), CancellationToken.None);

        await context.Carts.Where(c => c.SessionId == session)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.ExpiresAt, DateTime.Now.AddDays(1)));

        await using var later = fixture.CreateContext();
        await new AddCartItemHandler(later).Handle(new AddCartItemCommand(session, productId, Quantity: 1), CancellationToken.None);

        var cart = await later.Carts.AsNoTracking().SingleAsync(c => c.SessionId == session);
        Assert.True(cart.ExpiresAt > DateTime.Now.AddDays(29));
    }

    [Fact]
    public async Task Cleanup_DeletesExpiredCartsWithTheirItems_AndNothingElse()
    {
        await using var context = fixture.CreateContext();
        var productId = await CreateProductAsync(context);
        var now = DateTime.Now;

        var expired = new Cart { SessionId = CartSessionId.New(), ExpiresAt = now.AddMinutes(-1), CreatedAt = now, UpdatedAt = now };
        var live = new Cart { SessionId = CartSessionId.New(), ExpiresAt = now.AddDays(30), CreatedAt = now, UpdatedAt = now };
        expired.CartItems.Add(new CartItem { ProductId = productId, Quantity = 1, UnitPrice = 1000m, CreatedAt = now, UpdatedAt = now });
        context.Carts.AddRange(expired, live);
        await context.SaveChangesAsync();

        var deleted = await ExpiredCartCleanupService.DeleteExpiredAsync(context, now, CancellationToken.None);

        Assert.True(deleted >= 1);
        Assert.False(await context.Carts.AnyAsync(c => c.Id == expired.Id));
        Assert.False(await context.CartItems.AnyAsync(ci => ci.CartId == expired.Id));
        Assert.True(await context.Carts.AnyAsync(c => c.Id == live.Id));
    }

    private static async Task<long> CreateProductAsync(backend.Infrastructure.Persistence.AppDbContext context)
    {
        var product = await new CreateProductHandler(context).Handle(
            new CreateProductCommand("Session Product " + Guid.NewGuid(), null, null, null, Price: 1000, StockQuantity: 50),
            CancellationToken.None);
        return product.Value!.Id;
    }
}
