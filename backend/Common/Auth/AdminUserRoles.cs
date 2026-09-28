namespace backend.Common.Auth;

/// <summary>
/// The three roles the panel issues. `role` is a free-text column, so this is the
/// single place that decides which values the API accepts.
/// </summary>
public static class AdminUserRoles
{
    public const string SuperAdmin = "superadmin";
    public const string Manager = "manager";
    public const string Staff = "staff";

    public static readonly string[] All = [SuperAdmin, Manager, Staff];

    public static bool IsValid(string? role) =>
        role is not null && All.Contains(role, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string role) => role.Trim().ToLowerInvariant();
}
