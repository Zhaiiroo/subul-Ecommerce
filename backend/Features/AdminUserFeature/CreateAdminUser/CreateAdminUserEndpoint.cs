using backend.Common.Auth;
using backend.Common.Extensions;
using backend.Common.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Features.AdminUserFeature.CreateAdminUser;

[ApiController]
[Route("api/admin-users")]
[Tags("AdminUsers")]
[Authorize(Roles = AdminUserRoles.SuperAdmin)]
public class CreateAdminUserController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CreateAdminUserResponse>>> CreateAdminUser(
        [FromBody] CreateAdminUserCommand command)
    {
        var result = await sender.Send(command);
        return result.ToActionResult(StatusCodes.Status201Created);
    }
}
