using backend.Common.Auth;
using backend.Common.Extensions;
using backend.Common.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Features.AdminUserFeature.GetByIdAdminUser;

[ApiController]
[Route("api/admin-users")]
[Tags("AdminUsers")]
[Authorize(Roles = AdminUserRoles.SuperAdmin)]
public class GetByIdAdminUserController(ISender sender) : ControllerBase
{
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<GetByIdAdminUserResponse>>> GetByIdAdminUser(long id)
    {
        var result = await sender.Send(new GetByIdAdminUserQuery(id));
        return result.ToActionResult();
    }
}
