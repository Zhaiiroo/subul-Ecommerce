using System.Security.Claims;
using backend.Common.Extensions;
using backend.Common.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace backend.Features.AdminUserFeature.ChangeAdminUserPassword;

[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public class ChangeAdminUserPasswordController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Deliberately on api/auth rather than api/admin-users: every signed-in role
    /// changes their own password, and PasswordChangeRequiredMiddleware keeps this
    /// route open while the rest of the API is closed to them.
    /// </summary>
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<ChangeAdminUserPasswordResponse>>> ChangeAdminUserPassword(
        [FromBody] ChangeAdminUserPasswordRequest request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var adminUserId))
        {
            return Unauthorized(new ApiResponse<ChangeAdminUserPasswordResponse>(
                false,
                default,
                "unauthorized"));
        }

        var command = new ChangeAdminUserPasswordCommand(
            adminUserId,
            request.CurrentPassword,
            request.NewPassword);

        var result = await sender.Send(command);
        return result.ToActionResult();
    }
}
