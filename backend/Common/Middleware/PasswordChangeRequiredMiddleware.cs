using backend.Common.Auth;
using backend.Common.Responses;

namespace backend.Common.Middleware;

/// <summary>
/// While an account owes a password change, its token opens exactly three doors:
/// read your own profile, change your password, sign out. Enforcing it here
/// rather than in the panel means a temporary password is worth nothing to
/// anyone holding it outside the browser.
/// </summary>
public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    /// <summary>Message text, and the phrase the admin panel matches on to redirect.</summary>
    public const string ErrorMessage = "Password change required";

    private static readonly string[] AllowedPaths =
    [
        "/api/auth/me",
        "/api/auth/change-password",
        "/api/auth/logout",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!RequiresPasswordChange(context) || IsAllowed(context.Request.Path))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(
            new ApiResponse<object>(false, null, ErrorMessage),
            context.RequestAborted);
    }

    private static bool RequiresPasswordChange(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true &&
        string.Equals(
            context.User.FindFirst(AdminSessionClaims.MustChangePassword)?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowed(PathString path) =>
        !path.StartsWithSegments("/api") ||
        AllowedPaths.Any(allowed => path.Equals(allowed, StringComparison.OrdinalIgnoreCase));
}
