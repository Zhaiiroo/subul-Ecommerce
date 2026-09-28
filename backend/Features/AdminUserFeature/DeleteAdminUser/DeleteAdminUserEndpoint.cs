using System.Security.Claims;
using backend.Common.Auth;
using backend.Common.Extensions;
using backend.Common.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Features.AdminUserFeature.DeleteAdminUser;

[ApiController]
[Route("api/admin-users/{id:long}")]
[Tags("AdminUsers")]
[Authorize(Roles = AdminUserRoles.SuperAdmin)]
public class DeleteAdminUserController(ISender sender) : ControllerBase
{
    [HttpDelete]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteAdminUser(long id)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdClaim, out var currentAdminUserId))
        {
            return Unauthorized(new ApiResponse<bool>(false, default, "unauthorized"));
        }

        var result = await sender.Send(new DeleteAdminUserCommand(id, currentAdminUserId));
        return result.ToActionResult();
    }
}
