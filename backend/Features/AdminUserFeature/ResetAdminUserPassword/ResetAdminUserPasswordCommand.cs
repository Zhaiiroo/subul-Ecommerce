using backend.Common.Results;
using MediatR;

namespace backend.Features.AdminUserFeature.ResetAdminUserPassword;

/// <summary>
/// The "forgot my password" path: a superadmin issues a new credential for
/// someone else's account. <paramref name="NewPassword"/> is optional — omitted,
/// the API generates one.
/// </summary>
public record ResetAdminUserPasswordCommand(
    long Id,
    string? NewPassword = null) : IRequest<Result<ResetAdminUserPasswordResponse>>;

public record ResetAdminUserPasswordRequest(string? NewPassword = null);

public record ResetAdminUserPasswordResponse(
    long Id,
    string Email,
    bool MustChangePassword,
    DateTime? PasswordChangedAt,
    // Returned once. Null when the caller supplied the password themselves.
    string? TemporaryPassword);
