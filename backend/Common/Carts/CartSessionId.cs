namespace backend.Common.Carts;

/// <summary>
/// The guest cart key sent in the X-Cart-Session header. The server mints it
/// (a 32-character GUID, "N" format) on the first add-to-cart and the storefront
/// only ever echoes it back, so anything else is not a session this server
/// issued. Rejecting it up front keeps arbitrary strings — of any length — out
/// of the carts table and out of every cart and checkout query.
/// </summary>
public static class CartSessionId
{
    public const string InvalidMessage = "Invalid cart session";

    public static string New() => Guid.NewGuid().ToString("N");

    /// <summary>True when <paramref name="value"/> is a session id this server could have issued.</summary>
    public static bool TryNormalize(string? value, out string sessionId)
    {
        var trimmed = value?.Trim();
        if (trimmed is not null && Guid.TryParseExact(trimmed, "N", out var parsed))
        {
            sessionId = parsed.ToString("N");
            return true;
        }

        sessionId = string.Empty;
        return false;
    }
}
