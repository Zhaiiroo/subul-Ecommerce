using backend.Common.Auth;
using backend.Common.Results;
using backend.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.AdminUserFeature.UpdateAdminUser;

public class UpdateAdminUserValidator : AbstractValidator<UpdateAdminUserCommand>
{
    public UpdateAdminUserValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.Role)
            .NotEmpty()
            .MaximumLength(50);
    }
}

public class UpdateAdminUserHandler(AppDbContext context)
    : IRequestHandler<UpdateAdminUserCommand, Result<UpdateAdminUserResponse>>
{
    public async Task<Result<UpdateAdminUserResponse>> Handle(
        UpdateAdminUserCommand command,
        CancellationToken cancellationToken)
    {
        var adminUser = await context.AdminUsers
            .FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);

        if (adminUser is null)
            return Result<UpdateAdminUserResponse>.Failure("Admin user not found");

        if (!AdminUserRoles.IsValid(command.Role))
            return Result<UpdateAdminUserResponse>.Failure(
                $"Role must be one of: {string.Join(", ", AdminUserRoles.All)}");

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        var emailTaken = await context.AdminUsers.AnyAsync(
            u => u.Id != command.Id && u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (emailTaken)
            return Result<UpdateAdminUserResponse>.Failure("Admin user email already exists");

        var role = AdminUserRoles.Normalize(command.Role);
        var losesSuperAdmin =
            adminUser.Role.Equals(AdminUserRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) &&
            role != AdminUserRoles.SuperAdmin;

        if (losesSuperAdmin)
        {
            // Demoting yourself, or the only remaining superadmin, locks this screen
            // for everyone: nothing else can create or promote an admin account.
            if (adminUser.Id == command.CurrentAdminUserId)
                return Result<UpdateAdminUserResponse>.Failure("You cannot change your own role");

            var otherSuperAdmins = await context.AdminUsers.CountAsync(
                u => u.Id != command.Id &&
                     u.IsActive &&
                     u.Role.ToLower() == AdminUserRoles.SuperAdmin,
                cancellationToken);

            if (otherSuperAdmins == 0)
                return Result<UpdateAdminUserResponse>.Failure(
                    "Cannot change the role of the last active superadmin");
        }

        adminUser.Name = command.Name.Trim();
        adminUser.Email = normalizedEmail;
        adminUser.Role = role;
        adminUser.UpdatedAt = DateTime.Now;

        await context.SaveChangesAsync(cancellationToken);

        var response = new UpdateAdminUserResponse(
            adminUser.Id,
            adminUser.Name,
            adminUser.Email,
            adminUser.Role,
            adminUser.IsActive,
            adminUser.MustChangePassword,
            adminUser.LastLoginAt,
            adminUser.PasswordChangedAt,
            adminUser.CreatedAt,
            adminUser.UpdatedAt);

        return Result<UpdateAdminUserResponse>.Success(response);
    }
}
