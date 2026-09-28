using backend.Common.Results;
using backend.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.AdminUserFeature.GetByIdAdminUser;

public class GetByIdAdminUserHandler(AppDbContext context)
    : IRequestHandler<GetByIdAdminUserQuery, Result<GetByIdAdminUserResponse>>
{
    public async Task<Result<GetByIdAdminUserResponse>> Handle(
        GetByIdAdminUserQuery query,
        CancellationToken cancellationToken)
    {
        var adminUser = await context.AdminUsers
            .AsNoTracking()
            .Where(u => u.Id == query.Id)
            .Select(u => new GetByIdAdminUserResponse(
                u.Id,
                u.Name,
                u.Email,
                u.Role,
                u.IsActive,
                u.MustChangePassword,
                u.LastLoginAt,
                u.PasswordChangedAt,
                u.CreatedAt,
                u.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (adminUser is null)
            return Result<GetByIdAdminUserResponse>.Failure("Admin user not found");

        return Result<GetByIdAdminUserResponse>.Success(adminUser);
    }
}
