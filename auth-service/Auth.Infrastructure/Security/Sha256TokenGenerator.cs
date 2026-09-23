namespace Ouroboros.Auth.Infrastructure.Security;

using System.Security.Cryptography;
using System.Text;
using Ouroboros.Auth.Application.Gateways;

public sealed class Sha256TokenGenerator : ITokenGenerator
{
    private const int TokenSizeInBytes = 32;

    public string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenSizeInBytes);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    // Token aleatorio de 256 bits nao precisa de salt nem de derivacao lenta como senha:
    // SHA-256 simples ja e suficiente e permite buscar o token pelo hash com indice unico.
    public string Hash(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(hash);
    }
}
