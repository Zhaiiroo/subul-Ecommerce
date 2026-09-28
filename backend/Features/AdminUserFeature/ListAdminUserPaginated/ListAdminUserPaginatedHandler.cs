using backend.Common.Results;
using backend.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.AdminUserFeature.ListAdminUserPaginated;

public class ListAdminUserPaginatedHandler(AppDbContext context)
    : IRequestHandler<ListAdminUserPaginatedQuery, Result<ListAdminUserPaginatedResponse>>
{
    public async Task<Result<ListAdminUserPaginatedResponse>> Handle(
        ListAdminUserPaginatedQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var limit = Math.Clamp(query.Limit <= 0 ? 10 : query.Limit, 1, 100);

        var adminUserQuery = context.AdminUsers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            adminUserQuery = adminUserQuery.Where(u =>
                u.Name.ToLower().Contains(search) ||
                u.Email.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var role = query.Role.Trim().ToLower();
            adminUserQuery = adminUserQuery.Where(u => u.Role.ToLower() == role);
        }

        if (query.IsActive is not null)
            adminUserQuery = adminUserQuery.Where(u => u.IsActive == query.IsActive);

        adminUserQuery = (query.SortBy?.ToLower(), query.SortOrder?.ToLower()) switch
        {
            ("name", "asc") => adminUserQuery.OrderBy(u => u.Name),
            ("name", "desc") => adminUserQuery.OrderByDescending(u => u.Name),
            ("email", "asc") => adminUserQuery.OrderBy(u => u.Email),
            ("email", "desc") => adminUserQuery.OrderByDescending(u => u.Email),
            ("lastloginat", "asc") => adminUserQuery.OrderBy(u => u.LastLoginAt),
            ("lastloginat", "desc") => adminUserQuery.OrderByDescending(u => u.LastLoginAt),
            ("createdat", "asc") => adminUserQuery.OrderBy(u => u.CreatedAt),
            _ => adminUserQuery.OrderByDescending(u => u.CreatedAt)
        };

        var total = await adminUserQuery.CountAsync(cancellationToken);

        // Projected column by column: the entity carries PasswordHash, and a
        // Select over the whole row is how that ends up on the wire by accident.
        var items = await adminUserQuery
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(u => new ListAdminUserPaginatedItemResponse(
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
            .ToListAsync(cancellationToken);

        var response = new ListAdminUserPaginatedResponse(
            items,
            total,
            page,
            limit,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)limit));

        return Result<ListAdminUserPaginatedResponse>.Success(response);
    }
}
