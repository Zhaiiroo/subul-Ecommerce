using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using backend.Common.Extensions;
using backend.Common.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace backend.Features.AdminUserFeature.LogoutAdminUser;

[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public class LogoutAdminUserController(ISender sender) : ControllerBase
{
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<LogoutAdminUserResponse>>> LogoutAdminUser()
    {
        var expiresAt = long.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Exp), out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp)
            : (DateTimeOffset?)null;

        var result = await sender.Send(new LogoutAdminUserCommand(
            User.FindFirstValue(JwtRegisteredClaimNames.Jti),
            expiresAt));
        return result.ToActionResult();
    }
}
