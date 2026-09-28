namespace backend.Common.Auth;

/// <summary>
/// The one account a fresh production database starts with. Bound from the
/// <c>AdminBootstrap</c> configuration section; in Docker the password arrives as
/// <c>AdminBootstrap__Password</c>.
/// </summary>
public sealed class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    public bool Enabled { get; init; } = true;

    public string Name { get; init; } = "Administrator";

    public string Email { get; init; } = "admin@subul.iq";

    /// <summary>
    /// Left unset in every checked-in file on purpose. When it is missing the
    /// bootstrapper generates one and writes it to the log once, which is the
    /// only time it can ever be read: only the bcrypt hash is stored.
    /// </summary>
    public string? Password { get; init; }
}
