using backend.Common.Auth;
using backend.Common.Results;
using backend.Domain.Entities;
using backend.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.AdminUserFeature.CreateAdminUser;

public class CreateAdminUserValidator : AbstractValidator<CreateAdminUserCommand>
{
    public CreateAdminUserValidator()
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

public class CreateAdminUserHandler(AppDbContext context)
    : IRequestHandler<CreateAdminUserCommand, Result<CreateAdminUserResponse>>
{
    public async Task<Result<CreateAdminUserResponse>> Handle(
        CreateAdminUserCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        if (!AdminUserRoles.IsValid(command.Role))
            return Result<CreateAdminUserResponse>.Failure(
                $"Role must be one of: {string.Join(", ", AdminUserRoles.All)}");

        var emailExists = await context.AdminUsers.AnyAsync(
            u => u.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (emailExists)
            return Result<CreateAdminUserResponse>.Failure("Admin user email already exists");

        // A caller-supplied password is honoured but still has to clear the policy;
        // an omitted one is generated here and returned exactly once.
        var generated = string.IsNullOrWhiteSpace(command.Password);
        var password = generated ? AdminPasswordPolicy.Generate() : command.Password!.Trim();

        var policyError = AdminPasswordPolicy.Validate(password);
        if (policyError is not null)
            return Result<CreateAdminUserResponse>.Failure(policyError);

        var now = DateTime.Now;

        var adminUser = new AdminUser
        {
            Name = command.Name.Trim(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = AdminUserRoles.Normalize(command.Role),
            IsActive = true,
            // Whoever created the account knows the password it starts with, so it
            // is a hand-off credential, not the owner's. It buys one sign-in.
            MustChangePassword = true,
            PasswordChangedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        context.AdminUsers.Add(adminUser);
        await context.SaveChangesAsync(cancellationToken);

        var response = new CreateAdminUserResponse(
            adminUser.Id,
            adminUser.Name,
            adminUser.Email,
            adminUser.Role,
            adminUser.IsActive,
            adminUser.MustChangePassword,
            adminUser.CreatedAt,
            adminUser.UpdatedAt,
            generated ? password : null);

        return Result<CreateAdminUserResponse>.Success(response);
    }
}
