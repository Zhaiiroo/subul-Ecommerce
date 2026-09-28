using backend.Common.Auth;
using backend.Common.Results;
using backend.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.AdminUserFeature.ChangeAdminUserStatus;

public class ChangeAdminUserStatusHandler(AppDbContext context)
    : IRequestHandler<ChangeAdminUserStatusCommand, Result<ChangeAdminUserStatusResponse>>
{
    public async Task<Result<ChangeAdminUserStatusResponse>> Handle(
        ChangeAdminUserStatusCommand command,
        CancellationToken cancellationToken)
    {
        var adminUser = await context.AdminUsers
            .FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);

        if (adminUser is null)
            return Result<ChangeAdminUserStatusResponse>.Failure("Admin user not found");

        if (!command.IsActive)
        {
            if (adminUser.Id == command.CurrentAdminUserId)
                return Result<ChangeAdminUserStatusResponse>.Failure(
                    "You cannot deactivate your own account");

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
                    return Result<ChangeAdminUserStatusResponse>.Failure(
                        "Cannot deactivate the last active superadmin");
            }
        }

        adminUser.IsActive = command.IsActive;
        adminUser.UpdatedAt = DateTime.Now;

        await context.SaveChangesAsync(cancellationToken);

        // No token bookkeeping is needed here: AdminSessionValidator re-reads
        // is_active on every authenticated request, so the account is locked out
        // of its existing session on its very next call.
        var response = new ChangeAdminUserStatusResponse(
            adminUser.Id,
            adminUser.IsActive,
            adminUser.UpdatedAt);

        return Result<ChangeAdminUserStatusResponse>.Success(response);
    }
}
