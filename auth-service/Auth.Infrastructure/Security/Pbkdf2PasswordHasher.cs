namespace Ouroboros.Auth.Infrastructure.Security;

using System.Security.Cryptography;
using Ouroboros.Auth.Application.Gateways;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSizeInBytes = 16;
    private const int HashSizeInBytes = 32;
    private const int Iterations = 100_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeInBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeInBytes);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}
