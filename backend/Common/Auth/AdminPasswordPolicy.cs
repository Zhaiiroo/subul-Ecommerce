using System.Security.Cryptography;
using backend.Domain.Entities;

namespace backend.Common.Auth;

/// <summary>
/// One definition of "an acceptable admin password", shared by the bootstrapper,
/// the create/reset handlers and self-service change. Failure text is phrased so
/// <c>ResultExtensions.MapErrorToStatusCode</c> leaves it a 400.
/// </summary>
public static class AdminPasswordPolicy
{
    public const int MinimumLength = 10;
    public const int MaximumLength = 128;

    // Ambiguous glyphs are left out: generated passwords get read off a screen
    // and typed by hand, and 0/O and 1/l/I are where that goes wrong.
    private const string LowerAlphabet = "abcdefghijkmnopqrstuvwxyz";
    private const string UpperAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string DigitAlphabet = "23456789";
    private const string SymbolAlphabet = "!@#$%*?-_";
    private const string FullAlphabet = LowerAlphabet + UpperAlphabet + DigitAlphabet + SymbolAlphabet;

    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Password is required";

        if (password.Length < MinimumLength)
            return $"Password must be at least {MinimumLength} characters";

        if (password.Length > MaximumLength)
            return $"Password must be at most {MaximumLength} characters";

        if (!password.Any(char.IsUpper))
            return "Password must contain an uppercase letter";

        if (!password.Any(char.IsLower))
            return "Password must contain a lowercase letter";

        if (!password.Any(char.IsDigit))
            return "Password must contain a digit";

        return null;
    }

    /// <summary>
    /// A temporary password for a new account or an admin-issued reset. Every
    /// required character class is placed first and the result shuffled, so the
    /// output always satisfies <see cref="Validate"/>.
    /// </summary>
    public static string Generate(int length = 14)
    {
        length = Math.Clamp(length, MinimumLength, 32);

        var characters = new char[length];
        characters[0] = Pick(UpperAlphabet);
        characters[1] = Pick(LowerAlphabet);
        characters[2] = Pick(DigitAlphabet);
        characters[3] = Pick(SymbolAlphabet);

        for (var i = 4; i < length; i++)
            characters[i] = Pick(FullAlphabet);

        RandomNumberGenerator.Shuffle<char>(characters);
        return new string(characters);
    }

    /// <summary>
    /// The value written into <see cref="AdminSessionClaims.PasswordStamp"/>.
    /// Accounts that predate the column fall back to <c>CreatedAt</c> so the
    /// stamp is never absent.
    /// </summary>
    public static string StampFor(AdminUser user) =>
        (user.PasswordChangedAt ?? user.CreatedAt).Ticks.ToString();

    public static string StampFor(DateTime? passwordChangedAt, DateTime createdAt) =>
        (passwordChangedAt ?? createdAt).Ticks.ToString();

    private static char Pick(string alphabet) =>
        alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
