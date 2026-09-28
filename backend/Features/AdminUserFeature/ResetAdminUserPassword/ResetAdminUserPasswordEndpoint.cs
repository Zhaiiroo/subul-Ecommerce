using backend.Common.Auth;
using backend.Common.Extensions;
using backend.Common.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Features.AdminUserFeature.ResetAdminUserPassword;

[ApiController]
[Route("api/admin-users")]
[Tags("AdminUsers")]
[Authorize(Roles = AdminUserRoles.SuperAdmin)]
public class ResetAdminUserPasswordController(ISender sender) : ControllerBase
{
    [HttpPost("{id:long}/reset-password")]
    public async Task<ActionResult<ApiResponse<ResetAdminUserPasswordResponse>>> ResetAdminUserPassword(
        long id,
        [FromBody] ResetAdminUserPasswordRequest? request)
    {
        var command = new ResetAdminUserPasswordCommand(id, request?.NewPassword);
        var result = await sender.Send(command);
        return result.ToActionResult();
    }
}
