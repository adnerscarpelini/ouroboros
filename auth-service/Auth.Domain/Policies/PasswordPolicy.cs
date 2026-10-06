namespace Ouroboros.Auth.Domain.Policies;

using System.Text;
using Ouroboros.Auth.Domain.Exceptions;

/// <summary>
/// Regra de senha (NIST SP 800-63B) compartilhada por cadastro, redefinicao e troca. Fica no dominio porque a
/// <c>User</c> so recebe o hash — a senha em texto puro precisa ser validada antes de chegar na entidade.
/// A checagem de senhas vazadas (rede) fica fora daqui, em <c>IBreachedPasswordChecker</c>.
/// </summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 15;
    public const int MaximumLength = 128;

    public const string CommonOrBreachedMessage = "Password is too common or has appeared in a data breach";

    // Login e parte local do e-mail menores que isso gerariam falso positivo em quase toda senha.
    private const int MinimumContextWordLength = 4;
    private const string ServiceName = "ouroboros";

    // Lista embutida de senhas comuns com 15 ou mais caracteres (as menores ja caem no tamanho minimo).
    // Fonte: SecLists, Passwords/Common-Credentials/Pwdb_top-1000000.txt (licenca MIT).
    private static readonly Lazy<HashSet<string>> CommonPasswords = new(LoadCommonPasswords);

    /// <summary>
    /// NFKC, recomendado pelo NIST: a mesma senha digitada em teclados diferentes gera o mesmo hash.
    /// Senhas ASCII nao mudam.
    /// </summary>
    public static string Normalize(string password)
    {
        return password.Normalize(NormalizationForm.FormKC);
    }

    public static void Validate(
        string? password,
        string login,
        string email)
    {
        var normalized = Normalize(password ?? string.Empty);
        var length = normalized.EnumerateRunes().Count();

        if (length < MinimumLength)
        {
            throw new DomainException($"Password must be at least {MinimumLength} characters long");
        }

        if (length > MaximumLength)
        {
            throw new DomainException($"Password must be at most {MaximumLength} characters long");
        }

        if (CommonPasswords.Value.Contains(normalized.ToLowerInvariant()))
        {
            throw new DomainException(CommonOrBreachedMessage);
        }

        if (ContainsContextWord(normalized, login, email))
        {
            throw new DomainException("Password must not contain the login, the e-mail or the service name");
        }
    }

    private static bool ContainsContextWord(
        string password,
        string? login,
        string? email)
    {
        var words = new List<string> { ServiceName };

        if (login?.Trim().Length >= MinimumContextWordLength)
        {
            words.Add(login.Trim());
        }

        var localPart = email?.Split('@')[0].Trim();

        if (localPart?.Length >= MinimumContextWordLength)
        {
            words.Add(localPart);
        }

        return words.Any(word => password.Contains(Normalize(word), StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> LoadCommonPasswords()
    {
        using var stream = typeof(PasswordPolicy).Assembly.GetManifestResourceStream("common-passwords.txt")
            ?? throw new InvalidOperationException("Embedded resource common-passwords.txt not found.");
        using var reader = new StreamReader(stream);

        var passwords = new HashSet<string>(StringComparer.Ordinal);

        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                passwords.Add(line);
            }
        }

        return passwords;
    }
}
