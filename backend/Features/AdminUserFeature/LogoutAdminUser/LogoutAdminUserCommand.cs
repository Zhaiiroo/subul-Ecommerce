using backend.Common.Results;
using MediatR;

namespace backend.Features.AdminUserFeature.LogoutAdminUser;

/// <param name="TokenId">The caller's <c>jti</c>.</param>
/// <param name="ExpiresAt">The caller's <c>exp</c>: how long the revocation must be remembered.</param>
public record LogoutAdminUserCommand(string? TokenId, DateTimeOffset? ExpiresAt)
    : IRequest<Result<LogoutAdminUserResponse>>;

public record LogoutAdminUserResponse(bool Success);
