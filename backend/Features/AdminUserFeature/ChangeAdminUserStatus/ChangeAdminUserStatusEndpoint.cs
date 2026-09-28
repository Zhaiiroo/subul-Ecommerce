using System.Security.Claims;
using backend.Common.Auth;
using backend.Common.Extensions;
using backend.Common.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Features.AdminUserFeature.ChangeAdminUserStatus;

[ApiController]
[Route("api/admin-users")]
[Tags("AdminUsers")]
[Authorize(Roles = AdminUserRoles.SuperAdmin)]
public class ChangeAdminUserStatusController(ISender sender) : ControllerBase
{
    [HttpPut("{id:long}/status")]
    public async Task<ActionResult<ApiResponse<ChangeAdminUserStatusResponse>>> ChangeAdminUserStatus(
        long id,
        [FromBody] ChangeAdminUserStatusRequest request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var currentAdminUserId))
        {
            return Unauthorized(new ApiResponse<ChangeAdminUserStatusResponse>(
                false,
                default,
                "unauthorized"));
        }

        var command = new ChangeAdminUserStatusCommand(id, request.IsActive, currentAdminUserId);
        var result = await sender.Send(command);
        return result.ToActionResult();
    }
}
