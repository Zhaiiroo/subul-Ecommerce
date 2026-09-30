using backend.Common.Carts;
using backend.Common.Results;
using backend.Domain.Entities;
using backend.Features.CartFeature.GetCart;
using backend.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.CartFeature.UpdateCartItem;

public class UpdateCartItemHandler(AppDbContext context)
    : IRequestHandler<UpdateCartItemCommand, Result<CartResponse>>
{
    public async Task<Result<CartResponse>> Handle(
        UpdateCartItemCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Quantity < 1)
            return Result<CartResponse>.Failure("Quantity must be at least 1");

        if (string.IsNullOrWhiteSpace(command.SessionId))
            return Result<CartResponse>.Failure("Cart session is required");

        if (!CartSessionId.TryNormalize(command.SessionId, out var sessionId))
            return Result<CartResponse>.Failure(CartSessionId.InvalidMessage);

        var cartItem = await context.CartItems
            .Include(ci => ci.Cart)
            .Include(ci => ci.Product)
            .Include(ci => ci.Variant)
            .FirstOrDefaultAsync(ci => ci.Id == command.CartItemId, cancellationToken);

        if (cartItem is null)
            return Result<CartResponse>.Failure("Cart item not found");

        if (!BelongsToCaller(cartItem.Cart, sessionId))
            return Result<CartResponse>.Failure("Cart item not found");

        var availableStock = cartItem.Variant?.StockQuantity ?? cartItem.Product.StockQuantity;
        var minOrderQuantity = Math.Max(cartItem.Product.MinOrderQuantity, 1);

        if (command.Quantity < minOrderQuantity)
            return Result<CartResponse>.Failure($"Minimum order quantity is {minOrderQuantity}");

        if (command.Quantity > availableStock)
            return Result<CartResponse>.Failure("Insufficient stock");

        var now = DateTime.Now;
        cartItem.Quantity = command.Quantity;
        cartItem.UnitPrice = cartItem.Variant?.Price ?? cartItem.Product.Price;
        cartItem.UpdatedAt = now;
        cartItem.Cart.UpdatedAt = now;
        cartItem.Cart.ExpiresAt = now.Add(GetCartHandler.Lifetime);

        await context.SaveChangesAsync(cancellationToken);

        var response = await MapCartResponseAsync(context, cartItem.Cart, cancellationToken);
        return Result<CartResponse>.Success(response);
    }


    /// <summary>Ownership comes from the session header alone — a caller-supplied userId is not proof of identity.</summary>
    private static bool BelongsToCaller(Cart cart, string sessionId) =>
        string.Equals(cart.SessionId, sessionId, StringComparison.Ordinal);

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

        var imageByProduct = await GetCartHandler.GetPrimaryImagesAsync(context, items, cancellationToken);

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

        return new CartResponse(
            cart.Id,
            cart.SessionId ?? string.Empty,
            cart.UserId,
            cart.CouponCode,
            cart.Notes,
            mappedItems,
            mappedItems.Sum(i => i.LineTotal),
            mappedItems.Sum(i => i.Quantity));
    }
}
