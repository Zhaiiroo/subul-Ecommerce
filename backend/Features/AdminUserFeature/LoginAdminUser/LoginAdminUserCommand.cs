using backend.Common.Results;
using MediatR;

namespace backend.Features.AdminUserFeature.LoginAdminUser;

public record LoginAdminUserCommand(
    string Email,
    string Password) : IRequest<Result<LoginAdminUserResponse>>;

public record LoginAdminUserResponse(
    string AccessToken,
    AdminUserDto User);

public record AdminUserDto(
    long Id,
    string Name,
    string Email,
    string Role,
    bool MustChangePassword);
