using backend.Common.Results;
using backend.Features.AdminUserFeature.LoginAdminUser;
using backend.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.AdminUserFeature.GetCurrentAdminUser;

public class GetCurrentAdminUserHandler(AppDbContext context)
    : IRequestHandler<GetCurrentAdminUserQuery, Result<GetCurrentAdminUserResponse>>
{
    public async Task<Result<GetCurrentAdminUserResponse>> Handle(
        GetCurrentAdminUserQuery query,
        CancellationToken cancellationToken)
    {
        var user = await context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == query.AdminUserId, cancellationToken);

        if (user is null)
            return Result<GetCurrentAdminUserResponse>.Failure("Admin user not found");

        var response = new GetCurrentAdminUserResponse(
            new AdminUserDto(user.Id, user.Name, user.Email, user.Role, user.MustChangePassword));

        return Result<GetCurrentAdminUserResponse>.Success(response);
    }
}
