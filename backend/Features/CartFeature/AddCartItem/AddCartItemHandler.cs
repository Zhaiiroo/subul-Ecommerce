using backend.Common.Carts;
using backend.Common.Results;
using backend.Domain.Entities;
using backend.Features.CartFeature.GetCart;
using backend.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.CartFeature.AddCartItem;

public class AddCartItemHandler(AppDbContext context)
    : IRequestHandler<AddCartItemCommand, Result<AddCartItemResponse>>
{
    public async Task<Result<AddCartItemResponse>> Handle(
        AddCartItemCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Quantity < 1)
            return Result<AddCartItemResponse>.Failure("Quantity must be at least 1");

        // No session yet means a first add-to-cart: mint one. A session that is
        // present but malformed was not issued by this server.
        string sessionId;
        if (string.IsNullOrWhiteSpace(command.SessionId))
            sessionId = CartSessionId.New();
        else if (!CartSessionId.TryNormalize(command.SessionId, out sessionId))
            return Result<AddCartItemResponse>.Failure(CartSessionId.InvalidMessage);

        var product = await context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == command.ProductId, cancellationToken);

        if (product is null)
            return Result<AddCartItemResponse>.Failure("Product not found");

        if (!string.Equals(product.Status, "active", StringComparison.OrdinalIgnoreCase))
            return Result<AddCartItemResponse>.Failure("Product is not available");

        ProductVariant? variant = null;
        if (command.VariantId is not null)
        {
            variant = await context.ProductVariants
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    v => v.Id == command.VariantId && v.ProductId == command.ProductId,
                    cancellationToken);

            if (variant is null)
                return Result<AddCartItemResponse>.Failure("Product variant not found");

            if (!variant.IsActive)
                return Result<AddCartItemResponse>.Failure("Product variant is not available");
        }

        var availableStock = variant?.StockQuantity ?? product.StockQuantity;
        var minOrderQuantity = Math.Max(product.MinOrderQuantity, 1);

        if (command.Quantity < minOrderQuantity)
            return Result<AddCartItemResponse>.Failure($"Minimum order quantity is {minOrderQuantity}");

        var cart = await GetCartHandler.GetOrCreateCartAsync(context, sessionId, cancellationToken);

        var existingItem = await context.CartItems
            .FirstOrDefaultAsync(
                ci => ci.CartId == cart.Id
                      && ci.ProductId == command.ProductId
                      && ci.VariantId == command.VariantId,
                cancellationToken);

        var newQuantity = (existingItem?.Quantity ?? 0) + command.Quantity;

        if (newQuantity > availableStock)
            return Result<AddCartItemResponse>.Failure("Insufficient stock");

        var unitPrice = variant?.Price ?? product.Price;
        var now = DateTime.Now;

        if (existingItem is not null)
        {
            existingItem.Quantity = newQuantity;
            existingItem.UnitPrice = unitPrice;
            existingItem.UpdatedAt = now;
        }
        else
        {
            context.CartItems.Add(new CartItem
            {
                CartId = cart.Id,
                ProductId = command.ProductId,
                VariantId = command.VariantId,
                Quantity = command.Quantity,
                UnitPrice = unitPrice,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        cart.UpdatedAt = now;
        cart.ExpiresAt = now.Add(GetCartHandler.Lifetime);
        await context.SaveChangesAsync(cancellationToken);

        var cartResponse = await MapCartResponseAsync(context, cart, cancellationToken);
        return Result<AddCartItemResponse>.Success(new AddCartItemResponse(cartResponse, sessionId));
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
