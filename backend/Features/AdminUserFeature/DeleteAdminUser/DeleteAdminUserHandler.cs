using backend.Common.Auth;
using backend.Common.Results;
using backend.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.AdminUserFeature.DeleteAdminUser;

/// <summary>
/// Deletion is the narrow path. An admin account is the author of record on
/// order history, stock movements, returns and purchase orders, and removing the
/// row would either orphan or rewrite that history — so an account that has
/// touched anything can only be deactivated. Deleting stays available for the
/// case it is actually meant for: an account created by mistake.
/// </summary>
public class DeleteAdminUserHandler(AppDbContext context)
    : IRequestHandler<DeleteAdminUserCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        DeleteAdminUserCommand command,
        CancellationToken cancellationToken)
    {
        var adminUser = await context.AdminUsers
            .FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);

        if (adminUser is null)
            return Result<bool>.Failure("Admin user not found");

        if (adminUser.Id == command.CurrentAdminUserId)
            return Result<bool>.Failure("You cannot delete your own account");

        var isSuperAdmin = adminUser.Role.Equals(
            AdminUserRoles.SuperAdmin,
            StringComparison.OrdinalIgnoreCase);

        if (isSuperAdmin)
        {
            var otherActiveSuperAdmins = await context.AdminUsers.CountAsync(
                u => u.Id != command.Id &&
                     u.IsActive &&
                     u.Role.ToLower() == AdminUserRoles.SuperAdmin,
                cancellationToken);

            if (otherActiveSuperAdmins == 0)
                return Result<bool>.Failure("Cannot delete the last active superadmin");
        }

        if (await HasActivityAsync(command.Id, cancellationToken))
            return Result<bool>.Failure(
                "Cannot delete admin user because it is linked to existing records; deactivate the account instead");

        context.AdminUsers.Remove(adminUser);
        await context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }

    private async Task<bool> HasActivityAsync(long adminUserId, CancellationToken cancellationToken) =>
        await context.ActivityLogs.AnyAsync(x => x.AdminUserId == adminUserId, cancellationToken) ||
        await context.InventoryMovements.AnyAsync(x => x.AdminUserId == adminUserId, cancellationToken) ||
        await context.OrderStatusHistories.AnyAsync(x => x.AdminUserId == adminUserId, cancellationToken) ||
        await context.PurchaseOrders.AnyAsync(x => x.AdminUserId == adminUserId, cancellationToken) ||
        await context.CashCollections.AnyAsync(x => x.ReceivedBy == adminUserId, cancellationToken) ||
        await context.ContactMessages.AnyAsync(x => x.RepliedBy == adminUserId, cancellationToken) ||
        await context.FlashSales.AnyAsync(x => x.CreatedBy == adminUserId, cancellationToken) ||
        await context.OrderDeliveries.AnyAsync(x => x.AssignedBy == adminUserId, cancellationToken) ||
        await context.Returns.AnyAsync(x => x.ReviewedBy == adminUserId, cancellationToken) ||
        await context.WarrantyClaims.AnyAsync(x => x.HandledBy == adminUserId, cancellationToken);
}
