namespace Ouroboros.Auth.Domain.Policies;

using Ouroboros.Auth.Domain.Exceptions;

/// <summary>
/// Politica unica de identidade (spec 2026092508): como login e e-mail sao normalizados pra comparar e quais
/// formatos sao aceitos em cadastros novos. Mesmo papel do NormalizedUserName/NormalizedEmail do ASP.NET Identity.
/// </summary>
public static class IdentityPolicy
{
    public const int LoginMinLength = 3;
    public const int LoginMaxLength = 32;
    public const int EmailMaxLength = 254;

    // Trim + maiusculas invariantes. E esse valor que vai pras colunas normalized_* e pra toda comparacao.
    public static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    // Allowlist: A-Z, a-z, 0-9, '.', '_' e '-', comecando e terminando com letra ou digito.
    public static string ValidateLogin(string login)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            throw new DomainException("Login is required");
        }

        var trimmed = login.Trim();

        if (trimmed.Length < LoginMinLength || trimmed.Length > LoginMaxLength)
        {
            throw new DomainException($"Login must be between {LoginMinLength} and {LoginMaxLength} characters long");
        }

        if (!trimmed.All(IsAllowedLoginCharacter))
        {
            throw new DomainException("Login may only contain letters, digits, '.', '_' and '-'");
        }

        if (!IsAsciiLetterOrDigit(trimmed[0]) || !IsAsciiLetterOrDigit(trimmed[^1]))
        {
            throw new DomainException("Login must start and end with a letter or digit");
        }

        return trimmed;
    }

    public static string ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("A valid email is required");
        }

        var trimmed = email.Trim();

        if (trimmed.Length > EmailMaxLength)
        {
            throw new DomainException($"Email must be at most {EmailMaxLength} characters long");
        }

        if (trimmed.Any(char.IsWhiteSpace))
        {
            throw new DomainException("A valid email is required");
        }

        var parts = trimmed.Split('@');

        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            throw new DomainException("A valid email is required");
        }

        return trimmed;
    }

    private static bool IsAsciiLetterOrDigit(char character)
    {
        return character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
    }

    private static bool IsAllowedLoginCharacter(char character)
    {
        return IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-';
    }
}
