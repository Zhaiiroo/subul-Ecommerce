using backend.Common.Results;
using MediatR;

namespace backend.Features.AdminUserFeature.ChangeAdminUserPassword;

/// <summary>Self-service. The account id comes from the token, never the body.</summary>
public record ChangeAdminUserPasswordCommand(
    long AdminUserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result<ChangeAdminUserPasswordResponse>>;

public record ChangeAdminUserPasswordRequest(
    string CurrentPassword,
    string NewPassword);

public record ChangeAdminUserPasswordResponse(bool Success);
