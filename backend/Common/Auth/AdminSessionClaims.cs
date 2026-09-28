namespace backend.Common.Auth;

/// <summary>
/// Claims this API adds on top of the standard set. Both are re-checked against
/// the database on every authenticated request by <see cref="AdminSessionValidator"/>,
/// so a token never outlives the state it was minted from.
/// </summary>
public static class AdminSessionClaims
{
    /// <summary>Ticks of the account's password stamp at the time the token was issued.</summary>
    public const string PasswordStamp = "pwd_stamp";

    /// <summary>"true" while the account still owes a password change.</summary>
    public const string MustChangePassword = "must_change_password";
}
