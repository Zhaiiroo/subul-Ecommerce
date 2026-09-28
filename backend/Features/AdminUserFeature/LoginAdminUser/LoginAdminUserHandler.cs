using backend.Common.Auth;
using backend.Common.Results;
using backend.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.AdminUserFeature.LoginAdminUser;

public class LoginAdminUserValidator : AbstractValidator<LoginAdminUserCommand>
{
    public LoginAdminUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}

public class LoginAdminUserHandler(
    AppDbContext context,
    JwtTokenService jwtTokenService)
    : IRequestHandler<LoginAdminUserCommand, Result<LoginAdminUserResponse>>
{
    private static readonly string DummyPasswordHash =
        BCrypt.Net.BCrypt.HashPassword("dummy-timing-attack-mitigation");

    public async Task<Result<LoginAdminUserResponse>> Handle(
        LoginAdminUserCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var user = await context.AdminUsers
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        var passwordHash = user?.PasswordHash ?? DummyPasswordHash;
        bool passwordValid;
        try
        {
            passwordValid = BCrypt.Net.BCrypt.Verify(command.Password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Corrupt / non-bcrypt password_hash in DB — treat as failed login.
            passwordValid = false;
        }

        if (user is null || !passwordValid || !user.IsActive)
            return Result<LoginAdminUserResponse>.Failure("unauthorized");

        user.LastLoginAt = DateTime.Now;
        user.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync(cancellationToken);

        var accessToken = jwtTokenService.GenerateAccessToken(user);

        var response = new LoginAdminUserResponse(
            accessToken,
            new AdminUserDto(user.Id, user.Name, user.Email, user.Role, user.MustChangePassword));

        return Result<LoginAdminUserResponse>.Success(response);
    }
}
