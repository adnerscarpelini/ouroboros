namespace Ouroboros.Auth.Infrastructure.Security;

using System.Security.Cryptography;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Policies;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSizeInBytes = 16;
    private const int HashSizeInBytes = 32;

    // OWASP Password Storage Cheat Sheet: PBKDF2-HMAC-SHA256 com 600.000 iteracoes.
    private const int Iterations = 600_000;

    private static readonly string DummyHashValue = new Pbkdf2PasswordHasher().Hash(
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public string DummyHash => DummyHashValue;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeInBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(PasswordPolicy.Normalize(password), salt, Iterations, HashAlgorithmName.SHA256, HashSizeInBytes);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    // Usa as iteracoes e o salt gravados no proprio hash, pra continuar validando senhas antigas
    // mesmo se Iterations mudar no futuro.
    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.');

        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(PasswordPolicy.Normalize(password), salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public bool NeedsRehash(string passwordHash)
    {
        var parts = passwordHash.Split('.');

        return parts.Length != 3 || !int.TryParse(parts[0], out var iterations) || iterations < Iterations;
    }
}
