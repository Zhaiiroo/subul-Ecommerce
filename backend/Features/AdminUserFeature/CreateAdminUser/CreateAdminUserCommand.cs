using backend.Common.Results;
using MediatR;

namespace backend.Features.AdminUserFeature.CreateAdminUser;

/// <summary>
/// <paramref name="Password"/> is optional: leaving it out has the API generate a
/// temporary one, which is the path the panel takes.
/// </summary>
public record CreateAdminUserCommand(
    string Name,
    string Email,
    string Role,
    string? Password = null) : IRequest<Result<CreateAdminUserResponse>>;

public record CreateAdminUserResponse(
    long Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // The plaintext password, returned once at creation and never readable again —
    // only the bcrypt hash is stored. Null when the caller supplied the password.
    string? TemporaryPassword);
