using backend.Common.Carts;
using backend.Common.Results;
using backend.Domain.Entities;
using backend.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.CartFeature.GetCart;

public class GetCartHandler(AppDbContext context)
    : IRequestHandler<GetCartQuery, Result<CartResponse>>
{
    public async Task<Result<CartResponse>> Handle(
        GetCartQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.SessionId))
            return Result<CartResponse>.Failure("Cart session is required");

        if (!CartSessionId.TryNormalize(query.SessionId, out var sessionId))
            return Result<CartResponse>.Failure(CartSessionId.InvalidMessage);

        // A read never creates a row. It used to, which let an anonymous caller
        // mint a cart per request with nothing but a made-up header; the cart now
        // comes into existence on the first add-to-cart, and until then this is
        // the same empty cart the storefront would have shown anyway.
        var cart = await context.Carts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SessionId == sessionId, cancellationToken);

        if (cart is null)
            return Result<CartResponse>.Success(EmptyCart(sessionId));

        var response = await MapCartResponseAsync(context, cart, cancellationToken);
        return Result<CartResponse>.Success(response);
    }

    /// <summary>
    /// How long a guest cart lives after its last change. Every write moves the
    /// expiry forward, and <c>ExpiredCartCleanupService</c> deletes carts past it.
    /// </summary>
    internal static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private static CartResponse EmptyCart(string sessionId) =>
        new(0, sessionId, null, null, null, [], 0, 0);

    /// <summary>
    /// Carts are keyed by session only. Never resolve a cart from a caller-supplied
    /// userId, and never rebind an existing cart's SessionId — either lets an
    /// anonymous caller take over another user's cart.
    /// </summary>
    internal static async Task<Cart> GetOrCreateCartAsync(
        AppDbContext context,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var cart = await context.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.SessionId == sessionId, cancellationToken);

        if (cart is not null)
            return cart;

        var now = DateTime.Now;
        cart = new Cart
        {
            SessionId = sessionId,
            ExpiresAt = now.Add(Lifetime),
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Carts.Add(cart);
        await context.SaveChangesAsync(cancellationToken);

        return cart;
    }

    /// <summary>Primary image per product, shared by every slice that maps a cart.</summary>
    internal static async Task<Dictionary<long, string?>> GetPrimaryImagesAsync(
        AppDbContext context,
        IReadOnlyCollection<CartItem> items,
        CancellationToken cancellationToken)
    {
        var productIds = items.Select(ci => ci.ProductId).Distinct().ToList();
        if (productIds.Count == 0)
            return [];

        var rows = await context.ProductImages
            .AsNoTracking()
            .Where(pi => productIds.Contains(pi.ProductId))
            .Select(pi => new { pi.ProductId, pi.IsPrimary, pi.SortOrder, pi.ImageUrl })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(pi => pi.ProductId)
            .ToDictionary(
                g => g.Key,
                g => (string?)g
                    .OrderByDescending(pi => pi.IsPrimary)
                    .ThenBy(pi => pi.SortOrder)
                    .Select(pi => pi.ImageUrl)
                    .FirstOrDefault());
    }

    private static async Task<CartResponse> MapCartResponseAsync(
        AppDbContext context,
        Cart cart,
        CancellationToken cancellationToken)
    {
        var items = await context.CartItems
            .AsNoTracking()
            .Where(ci => ci.CartId == cart.Id)
            .Include(ci => ci.Product)
            .Include(ci => ci.Variant)
            .OrderBy(ci => ci.Id)
            .ToListAsync(cancellationToken);

        var imageByProduct = await GetPrimaryImagesAsync(context, items, cancellationToken);

        var mappedItems = items.Select(ci =>
        {
            var unitPrice = ci.UnitPrice ?? ci.Variant?.Price ?? ci.Product.Price;
            return new CartItemResponse(
                ci.Id,
                ci.ProductId,
                ci.VariantId,
                ci.Product.NameEn,
                ci.Product.NameAr,
                ci.Product.Slug,
                ci.Variant?.Sku ?? ci.Product.Sku,
                imageByProduct.GetValueOrDefault(ci.ProductId),
                ci.Quantity,
                unitPrice,
                unitPrice * ci.Quantity);
        }).ToList();

        var subtotal = mappedItems.Sum(i => i.LineTotal);
        var itemCount = mappedItems.Sum(i => i.Quantity);

        return new CartResponse(
            cart.Id,
            cart.SessionId ?? string.Empty,
            cart.UserId,
            cart.CouponCode,
            cart.Notes,
            mappedItems,
            subtotal,
            itemCount);
    }
}
