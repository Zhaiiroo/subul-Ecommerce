using backend.Common.Carts;
using backend.Common.Results;
using backend.Domain.Entities;
using backend.Features.CartFeature.GetCart;
using backend.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.CartFeature.RemoveCartItem;

public class RemoveCartItemHandler(AppDbContext context)
    : IRequestHandler<RemoveCartItemCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        RemoveCartItemCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.SessionId))
            return Result<bool>.Failure("Cart session is required");

        if (!CartSessionId.TryNormalize(command.SessionId, out var sessionId))
            return Result<bool>.Failure(CartSessionId.InvalidMessage);

        var cartItem = await context.CartItems
            .Include(ci => ci.Cart)
            .FirstOrDefaultAsync(ci => ci.Id == command.CartItemId, cancellationToken);

        if (cartItem is null)
            return Result<bool>.Failure("Cart item not found");

        if (!BelongsToCaller(cartItem.Cart, sessionId))
            return Result<bool>.Failure("Cart item not found");

        var now = DateTime.Now;
        cartItem.Cart.UpdatedAt = now;
        cartItem.Cart.ExpiresAt = now.Add(GetCartHandler.Lifetime);
        context.CartItems.Remove(cartItem);
        await context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }


    /// <summary>Ownership comes from the session header alone — a caller-supplied userId is not proof of identity.</summary>
    private static bool BelongsToCaller(Cart cart, string sessionId) =>
        string.Equals(cart.SessionId, sessionId, StringComparison.Ordinal);
}
