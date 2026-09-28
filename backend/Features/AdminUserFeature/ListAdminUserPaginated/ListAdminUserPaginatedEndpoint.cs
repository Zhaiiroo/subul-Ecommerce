using backend.Common.Auth;
using backend.Common.Extensions;
using backend.Common.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Features.AdminUserFeature.ListAdminUserPaginated;

[ApiController]
[Route("api/admin-users")]
[Tags("AdminUsers")]
[Authorize(Roles = AdminUserRoles.SuperAdmin)]
public class ListAdminUserPaginatedController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<ListAdminUserPaginatedResponse>>> ListAdminUsers(
        [FromQuery] ListAdminUserPaginatedQuery query)
    {
        var result = await sender.Send(query);
        return result.ToActionResult();
    }
}
