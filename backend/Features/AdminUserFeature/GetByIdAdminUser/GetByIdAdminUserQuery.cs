using backend.Common.Results;
using MediatR;

namespace backend.Features.AdminUserFeature.GetByIdAdminUser;

public record GetByIdAdminUserQuery(long Id) : IRequest<Result<GetByIdAdminUserResponse>>;

public record GetByIdAdminUserResponse(
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
