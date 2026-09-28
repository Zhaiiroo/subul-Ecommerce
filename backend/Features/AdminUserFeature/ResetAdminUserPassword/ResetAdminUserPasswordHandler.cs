using backend.Common.Auth;
using backend.Common.Results;
using backend.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.AdminUserFeature.ResetAdminUserPassword;

public class ResetAdminUserPasswordHandler(AppDbContext context)
    : IRequestHandler<ResetAdminUserPasswordCommand, Result<ResetAdminUserPasswordResponse>>
{
    public async Task<Result<ResetAdminUserPasswordResponse>> Handle(
        ResetAdminUserPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var adminUser = await context.AdminUsers
            .FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);

        if (adminUser is null)
            return Result<ResetAdminUserPasswordResponse>.Failure("Admin user not found");

        var generated = string.IsNullOrWhiteSpace(command.NewPassword);
        var password = generated ? AdminPasswordPolicy.Generate() : command.NewPassword!.Trim();

        var policyError = AdminPasswordPolicy.Validate(password);
        if (policyError is not null)
            return Result<ResetAdminUserPasswordResponse>.Failure(policyError);

        adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        // Moving the stamp is what makes this a real reset: every token already
        // issued to this account stops validating on its next request, so whoever
        // was signed in is signed out.
        adminUser.PasswordChangedAt = DateTime.Now;
        adminUser.MustChangePassword = true;
        adminUser.UpdatedAt = DateTime.Now;

        await context.SaveChangesAsync(cancellationToken);

        var response = new ResetAdminUserPasswordResponse(
            adminUser.Id,
            adminUser.Email,
            adminUser.MustChangePassword,
            adminUser.PasswordChangedAt,
            generated ? password : null);

        return Result<ResetAdminUserPasswordResponse>.Success(response);
    }
}
