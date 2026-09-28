using backend.Common.Results;
using MediatR;

namespace backend.Features.AdminUserFeature.UpdateAdminUser;

public record UpdateAdminUserCommand(
    long Id,
    string Name,
    string Email,
    string Role,
    long CurrentAdminUserId) : IRequest<Result<UpdateAdminUserResponse>>;

/// <summary>Body shape; <c>Id</c> and the caller come from the route and the token.</summary>
public record UpdateAdminUserRequest(
    string Name,
    string Email,
    string Role);

public record UpdateAdminUserResponse(
    long Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    bool MustChangePassword,
    DateTime? LastLoginAt,
    DateTime? PasswordChangedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
