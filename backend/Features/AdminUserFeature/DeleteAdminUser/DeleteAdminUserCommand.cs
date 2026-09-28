using backend.Common.Results;
using MediatR;

namespace backend.Features.AdminUserFeature.DeleteAdminUser;

public record DeleteAdminUserCommand(long Id, long CurrentAdminUserId) : IRequest<Result<bool>>;
