using backend.Common.Auth;
using backend.Common.Results;
using MediatR;
using StackExchange.Redis;

namespace backend.Features.AdminUserFeature.LogoutAdminUser;

/// <summary>
/// H-5 / H-2: sign-out used to return success and change nothing, so a token
/// copied out of the browser stayed valid for its full eight hours after the
/// user had signed out. It is now revoked: AdminSessionValidator refuses it on
/// the next request.
///
/// <paramref name="revocations"/> is absent only when no Redis is configured,
/// which production does not allow; the token then simply runs out.
/// </summary>
public class LogoutAdminUserHandler(TokenRevocationStore? revocations = null)
    : IRequestHandler<LogoutAdminUserCommand, Result<LogoutAdminUserResponse>>
{
    public async Task<Result<LogoutAdminUserResponse>> Handle(
        LogoutAdminUserCommand request,
        CancellationToken cancellationToken)
    {
        if (revocations is null)
            return Result<LogoutAdminUserResponse>.Success(new LogoutAdminUserResponse(true));

        // AdminSessionValidator rejects tokens without an id before they get here.
        if (string.IsNullOrEmpty(request.TokenId) || request.ExpiresAt is null)
            return Result<LogoutAdminUserResponse>.Failure("Token cannot be revoked");

        try
        {
            await revocations.RevokeAsync(request.TokenId, request.ExpiresAt.Value, cancellationToken);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            // Reported rather than swallowed: the panel signs out locally either
            // way, but a sign-out that did not revoke must not claim it did.
            return Result<LogoutAdminUserResponse>.Failure("Sign-out could not be completed");
        }

        return Result<LogoutAdminUserResponse>.Success(new LogoutAdminUserResponse(true));
    }
}
