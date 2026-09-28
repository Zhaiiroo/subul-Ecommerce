using backend.Common.Results;
using MediatR;

namespace backend.Features.AdminUserFeature.ChangeAdminUserStatus;

public record ChangeAdminUserStatusCommand(
    long Id,
    bool IsActive,
    long CurrentAdminUserId) : IRequest<Result<ChangeAdminUserStatusResponse>>;

public record ChangeAdminUserStatusRequest(bool IsActive);

public record ChangeAdminUserStatusResponse(
    long Id,
    bool IsActive,
    DateTime? UpdatedAt);
