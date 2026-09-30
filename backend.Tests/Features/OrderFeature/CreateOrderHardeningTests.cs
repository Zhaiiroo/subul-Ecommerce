using System.Text.RegularExpressions;
using backend.Common.Carts;
using backend.Features.CartFeature.AddCartItem;
using backend.Features.OrderFeature.CreateOrder;
using backend.Features.ProductFeature.CreateProduct;
using backend.Features.ShippingZoneFeature.CreateShippingZone;
using backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace backend.Tests.Features.OrderFeature;

/// <summary>
/// Regression tests for the Wave B order findings: H-4 (overselling under
/// concurrency), M-10 (orders at a price that no longer exists), M-3 (guessable
/// order numbers) and M-6 (coupon codes accepted and ignored), plus the
/// COD-only payment rule and the cart-session format check (M-13).
/// </summary>
[Collection("Database")]
public class CreateOrderHardeningTests(DatabaseFixture fixture)
{
    // H-4 ---------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentCheckouts_ForTheLastUnit_SellItExactlyOnce()
    {
        var (zoneId, productId) = await CreateZoneAndProductAsync(stock: 1, price: 1000m);
        var first = await CartWithAsync(productId, quantity: 1);
        var second = await CartWithAsync(productId, quantity: 1);

        // Both checkouts read stock = 1 and pass the up-front check, then queue
        // behind a lock on the product row. Released together, they race on the
        // decrement itself — the window the old read-then-write code lost.
        var results = await RaceBehindRowLockAsync(productId, [first, second], zoneId);

        Assert.Single(results, r => r.IsSuccess);
        Assert.Contains(results, r => !r.IsSuccess && r.Error!.Contains("Insufficient stock"));

        await using var context = fixture.CreateContext();
        var product = await context.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.Equal(0, product.StockQuantity);
        Assert.Equal(1, product.TotalSold);
    }

    [Fact]
    public async Task DoubleSubmit_OfTheSameCart_CreatesOneOrder()
    {
        var (zoneId, productId) = await CreateZoneAndProductAsync(stock: 10, price: 1000m);
        var session = await CartWithAsync(productId, quantity: 2);

        var results = await RaceBehindRowLockAsync(productId, [session, session], zoneId);

        Assert.Single(results, r => r.IsSuccess);

        await using var context = fixture.CreateContext();
        var product = await context.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.Equal(8, product.StockQuantity);
        Assert.Equal(1, await context.OrderItems.CountAsync(oi => oi.ProductId == productId));
    }

    // M-10 --------------------------------------------------------------------

    [Fact]
    public async Task PriceChangedSinceAddedToCart_AsksTheShopperToConfirm()
    {
        var (zoneId, productId) = await CreateZoneAndProductAsync(stock: 5, price: 1000m);
        var session = await CartWithAsync(productId, quantity: 2);

        await using (var admin = fixture.CreateContext())
        {
            await admin.Products.Where(p => p.Id == productId)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.Price, 1200m));
        }

        await using var context = fixture.CreateContext();
        var refused = await new CreateOrderHandler(context).Handle(Checkout(session, zoneId), CancellationToken.None);

        Assert.False(refused.IsSuccess);
        Assert.Contains("تغيّرت أسعار", refused.Error);

        // The cart now shows the new price, so confirming again is informed.
        await using var retryContext = fixture.CreateContext();
        var item = await retryContext.CartItems.AsNoTracking().SingleAsync(ci => ci.Cart.SessionId == session);
        Assert.Equal(1200m, item.UnitPrice);

        var placed = await new CreateOrderHandler(retryContext).Handle(Checkout(session, zoneId), CancellationToken.None);
        Assert.True(placed.IsSuccess);
        Assert.Equal(2400m, placed.Value!.Subtotal);
    }

    // M-3 ---------------------------------------------------------------------

    [Fact]
    public async Task OrderNumber_IsEightCharactersFromTheUnambiguousAlphabet()
    {
        var (zoneId, productId) = await CreateZoneAndProductAsync(stock: 5, price: 1000m);
        var session = await CartWithAsync(productId, quantity: 1);

        await using var context = fixture.CreateContext();
        var result = await new CreateOrderHandler(context).Handle(Checkout(session, zoneId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Matches(new Regex("^ORD-[0-9]{8}-[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{8}$"), result.Value!.OrderNumber);
    }

    // M-6 and payment ---------------------------------------------------------

    [Fact]
    public void CouponCode_IsRefusedUntilCouponsExist()
    {
        var validation = new CreateOrderValidator().Validate(
            Checkout(CartSessionId.New(), zoneId: 1) with { CouponCode = "SAVE10" });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.ErrorMessage == "الكوبونات غير متاحة حالياً");
    }

    [Theory]
    [InlineData("online")]
    [InlineData("bank_transfer")]
    public async Task PaymentMethodsWithNothingBehindThem_AreRefused(string paymentMethod)
    {
        var (zoneId, productId) = await CreateZoneAndProductAsync(stock: 5, price: 1000m);
        var session = await CartWithAsync(productId, quantity: 1);

        await using var context = fixture.CreateContext();
        var result = await new CreateOrderHandler(context).Handle(
            Checkout(session, zoneId) with { PaymentMethod = paymentMethod },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid payment method", result.Error);
    }

    // M-13 --------------------------------------------------------------------

    [Fact]
    public async Task MalformedCartSession_IsRefused()
    {
        await using var context = fixture.CreateContext();
        var result = await new CreateOrderHandler(context).Handle(
            Checkout("not-a-session-this-server-issued", zoneId: 1),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CartSessionId.InvalidMessage, result.Error);
    }

    // ------------------------------------------------------------------------

    private async Task<List<backend.Common.Results.Result<CreateOrderResponse>>> RaceBehindRowLockAsync(
        long productId,
        IReadOnlyList<string> sessions,
        long zoneId)
    {
        await using var holder = new NpgsqlConnection(fixture.ConnectionString);
        await holder.OpenAsync();
        await using var lockTransaction = await holder.BeginTransactionAsync();
        await using (var lockRow = new NpgsqlCommand("SELECT 1 FROM products WHERE id = @id FOR UPDATE", holder, lockTransaction))
        {
            lockRow.Parameters.AddWithValue("id", productId);
            await lockRow.ExecuteNonQueryAsync();
        }

        var checkouts = sessions.Select(async session =>
        {
            await using var context = fixture.CreateContext();
            return await new CreateOrderHandler(context).Handle(Checkout(session, zoneId), CancellationToken.None);
        }).ToList();

        // Long enough for every checkout to pass its reads and block on the lock.
        await Task.Delay(TimeSpan.FromSeconds(2));
        await lockTransaction.CommitAsync();

        return [.. await Task.WhenAll(checkouts)];
    }

    private async Task<(long ZoneId, long ProductId)> CreateZoneAndProductAsync(int stock, decimal price)
    {
        await using var context = fixture.CreateContext();

        var zone = await new CreateShippingZoneHandler(context).Handle(
            new CreateShippingZoneCommand(
                "Hardening Zone " + Guid.NewGuid(),
                Governorates: ["Baghdad"],
                ShippingRates: [new CreateShippingRateInput("Standard", null, "flat", 5000m, IsActive: true)]),
            CancellationToken.None);

        var product = await new CreateProductHandler(context).Handle(
            new CreateProductCommand("Hardening Product " + Guid.NewGuid(), null, null, null, Price: price, StockQuantity: stock),
            CancellationToken.None);

        return (zone.Value!.Id, product.Value!.Id);
    }

    private async Task<string> CartWithAsync(long productId, int quantity)
    {
        var session = CartSessionId.New();
        await using var context = fixture.CreateContext();
        var added = await new AddCartItemHandler(context).Handle(
            new AddCartItemCommand(session, productId, Quantity: quantity),
            CancellationToken.None);
        Assert.True(added.IsSuccess);
        return session;
    }

    private static CreateOrderCommand Checkout(string session, long zoneId) => new(
        SessionId: session,
        ShippingFirstName: "Ahmed",
        ShippingLastName: "Ali",
        ShippingPhone: "07701234567",
        ShippingAddress1: "Street 1",
        ShippingCity: "Baghdad",
        ShippingGovernorate: "Baghdad",
        ShippingZoneId: zoneId,
        PaymentMethod: "cod");
}
