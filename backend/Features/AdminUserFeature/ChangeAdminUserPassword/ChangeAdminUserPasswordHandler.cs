using backend.Common.Auth;
using backend.Common.Results;
using backend.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.AdminUserFeature.ChangeAdminUserPassword;

public class ChangeAdminUserPasswordValidator : AbstractValidator<ChangeAdminUserPasswordCommand>
{
    public ChangeAdminUserPasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}

public class ChangeAdminUserPasswordHandler(AppDbContext context)
    : IRequestHandler<ChangeAdminUserPasswordCommand, Result<ChangeAdminUserPasswordResponse>>
{
    public async Task<Result<ChangeAdminUserPasswordResponse>> Handle(
        ChangeAdminUserPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var adminUser = await context.AdminUsers
            .FirstOrDefaultAsync(u => u.Id == command.AdminUserId, cancellationToken);

        if (adminUser is null)
            return Result<ChangeAdminUserPasswordResponse>.Failure("Admin user not found");

        bool currentPasswordValid;
        try
        {
            currentPasswordValid = BCrypt.Net.BCrypt.Verify(command.CurrentPassword, adminUser.PasswordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Same treatment LoginAdminUserHandler gives a corrupt hash.
            currentPasswordValid = false;
        }

        if (!currentPasswordValid)
            return Result<ChangeAdminUserPasswordResponse>.Failure("Current password is incorrect");

        var policyError = AdminPasswordPolicy.Validate(command.NewPassword);
        if (policyError is not null)
            return Result<ChangeAdminUserPasswordResponse>.Failure(policyError);

        if (command.NewPassword == command.CurrentPassword)
            return Result<ChangeAdminUserPasswordResponse>.Failure(
                "New password must be different from the current password");

        adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.NewPassword);
        adminUser.PasswordChangedAt = DateTime.Now;
        adminUser.MustChangePassword = false;
        adminUser.UpdatedAt = DateTime.Now;

        await context.SaveChangesAsync(cancellationToken);

        // The stamp just moved, so the token that authorized this request is now
        // stale too. That is intentional — the caller signs in again with the
        // password they just chose, and any other device holding the old one is
        // signed out with them.
        return Result<ChangeAdminUserPasswordResponse>.Success(
            new ChangeAdminUserPasswordResponse(true));
    }
}
