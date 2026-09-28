using System.Security.Claims;
using backend.Common.Auth;
using backend.Common.Extensions;
using backend.Common.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Features.AdminUserFeature.UpdateAdminUser;

[ApiController]
[Route("api/admin-users")]
[Tags("AdminUsers")]
[Authorize(Roles = AdminUserRoles.SuperAdmin)]
public class UpdateAdminUserController(ISender sender) : ControllerBase
{
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<UpdateAdminUserResponse>>> UpdateAdminUser(
        long id,
        [FromBody] UpdateAdminUserRequest request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var currentAdminUserId))
        {
            return Unauthorized(new ApiResponse<UpdateAdminUserResponse>(
                false,
                default,
                "unauthorized"));
        }

        var command = new UpdateAdminUserCommand(
            id,
            request.Name,
            request.Email,
            request.Role,
            currentAdminUserId);

        var result = await sender.Send(command);
        return result.ToActionResult();
    }
}
