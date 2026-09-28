using backend.Common.Results;
using MediatR;

namespace backend.Features.AdminUserFeature.ListAdminUserPaginated;

public record ListAdminUserPaginatedQuery(
    int Page = 1,
    int Limit = 10,
    string? Search = null,
    string? Role = null,
    bool? IsActive = null,
    string? SortBy = "createdAt",
    string? SortOrder = "desc") : IRequest<Result<ListAdminUserPaginatedResponse>>;

public record ListAdminUserPaginatedResponse(
    List<ListAdminUserPaginatedItemResponse> Items,
    int Total,
    int Page,
    int Limit,
    int TotalPages);

public record ListAdminUserPaginatedItemResponse(
    long Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    bool MustChangePassword,
    DateTime? LastLoginAt,
    DateTime? PasswordChangedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
