namespace Ouroboros.Auth.Domain.Policies;

using Ouroboros.Auth.Domain.Exceptions;

/// <summary>
/// Regra de forca de senha compartilhada por cadastro e redefinicao. Fica no dominio porque a <c>User</c>
/// so recebe o hash — a senha em texto puro precisa ser validada antes de chegar na entidade.
/// </summary>
public static class PasswordPolicy
{
    private const int MinimumLength = 8;

    public static void Validate(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
        {
            throw new DomainException("Password must be at least 8 characters long");
        }

        if (!password.Any(char.IsUpper))
        {
            throw new DomainException("Password must contain at least one uppercase letter");
        }

        if (!password.Any(char.IsLower))
        {
            throw new DomainException("Password must contain at least one lowercase letter");
        }

        if (!password.Any(char.IsDigit))
        {
            throw new DomainException("Password must contain at least one digit");
        }

        if (password.All(char.IsLetterOrDigit))
        {
            throw new DomainException("Password must contain at least one special character");
        }
    }
}
