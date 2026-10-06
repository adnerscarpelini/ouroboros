namespace Ouroboros.Auth.Infrastructure.Security;

using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Chaves RSA de assinatura carregadas dos arquivos PEM. A privada fica so aqui, no auth-service: os outros servicos
/// validam com a publica (JWKS) e nao conseguem emitir token.
/// </summary>
public sealed class SigningKeyStore
{
    public const int MinimumRsaKeySizeInBits = 2048;

    private SigningKeyStore(
        SigningCredentials activeCredentials,
        IReadOnlyList<RsaSecurityKey> publicKeys,
        IReadOnlyList<PublicJsonWebKey> jsonWebKeys)
    {
        ActiveCredentials = activeCredentials;
        PublicKeys = publicKeys;
        JsonWebKeys = jsonWebKeys;
    }

    /// <summary>Credencial da chave <c>Active</c>: assina todo token novo (RS256, com o <c>kid</c> no header).</summary>
    public SigningCredentials ActiveCredentials { get; }

    /// <summary>Chaves publicas das <c>Active</c> e <c>Published</c>, as que validam token.</summary>
    public IReadOnlyList<RsaSecurityKey> PublicKeys { get; }

    /// <summary>As mesmas chaves publicas, em formato JWK (RFC 7517), prontas pro JWKS.</summary>
    public IReadOnlyList<PublicJsonWebKey> JsonWebKeys { get; }

    /// <summary>
    /// Confere a lista de chaves sem carregar nada alem do necessario. Devolve as mensagens de erro (vazia = valida).
    /// </summary>
    public static IReadOnlyList<string> Validate(IReadOnlyList<SigningKeySettings> keys)
    {
        var errors = new List<string>();

        if (keys.Count == 0)
        {
            errors.Add("Jwt:SigningKeys must have at least one key.");
            return errors;
        }

        foreach (var kid in keys.Select(key => key.Kid).Where(kid => string.IsNullOrWhiteSpace(kid)))
        {
            errors.Add("Every Jwt:SigningKeys entry needs a Kid.");
            break;
        }

        foreach (var duplicated in keys.Where(key => !string.IsNullOrWhiteSpace(key.Kid)).GroupBy(key => key.Kid, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            errors.Add($"Jwt:SigningKeys has a repeated Kid '{duplicated.Key}'.");
        }

        var active = keys.Count(key => key.Status == SigningKeyStatus.Active);

        if (active != 1)
        {
            errors.Add($"Jwt:SigningKeys must have exactly one Active key, found {active}.");
        }

        foreach (var key in keys)
        {
            using var rsa = TryLoad(key, errors);

            if (rsa is not null && rsa.KeySize < MinimumRsaKeySizeInBits)
            {
                errors.Add($"Jwt:SigningKeys '{key.Kid}' is a {rsa.KeySize}-bit RSA key; the minimum is {MinimumRsaKeySizeInBits} bits.");
            }
        }

        return errors;
    }

    /// <exception cref="InvalidOperationException">A lista de chaves e invalida (ver <see cref="Validate"/>).</exception>
    public static SigningKeyStore Load(IReadOnlyList<SigningKeySettings> keys)
    {
        var errors = Validate(keys);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        SigningCredentials? activeCredentials = null;
        var publicKeys = new List<RsaSecurityKey>();
        var jsonWebKeys = new List<PublicJsonWebKey>();

        foreach (var key in keys)
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(key.PrivateKeyPath));

            if (key.Status == SigningKeyStatus.Active)
            {
                // A RSA privada fica viva pelo tempo do processo (singleton): e ela que assina.
                activeCredentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = key.Kid }, SecurityAlgorithms.RsaSha256);
            }

            // So os parametros publicos saem daqui: o JWKS nunca expoe d, p, q, dp, dq nem qi.
            var parameters = rsa.ExportParameters(includePrivateParameters: false);

            publicKeys.Add(new RsaSecurityKey(parameters) { KeyId = key.Kid });
            jsonWebKeys.Add(new PublicJsonWebKey(
                "RSA",
                "sig",
                key.Kid,
                "RS256",
                Base64UrlEncoder.Encode(parameters.Modulus!),
                Base64UrlEncoder.Encode(parameters.Exponent!)));

            if (key.Status != SigningKeyStatus.Active)
            {
                rsa.Dispose();
            }
        }

        return new SigningKeyStore(activeCredentials!, publicKeys, jsonWebKeys);
    }

    private static RSA? TryLoad(
        SigningKeySettings key,
        List<string> errors)
    {
        var name = string.IsNullOrWhiteSpace(key.Kid) ? "(without Kid)" : key.Kid;

        if (string.IsNullOrWhiteSpace(key.PrivateKeyPath) || !File.Exists(key.PrivateKeyPath))
        {
            errors.Add($"Jwt:SigningKeys '{name}' has no readable PrivateKeyPath.");
            return null;
        }

        var rsa = RSA.Create();

        try
        {
            rsa.ImportFromPem(File.ReadAllText(key.PrivateKeyPath));
            return rsa;
        }
        catch (Exception e) when (e is CryptographicException or ArgumentException)
        {
            rsa.Dispose();
            errors.Add($"Jwt:SigningKeys '{name}' is not a valid RSA private key in PEM format.");
            return null;
        }
    }
}

/// <summary>Chave publica em formato JWK (RFC 7517). So parametros publicos.</summary>
public sealed record PublicJsonWebKey(
    string Kty,
    string Use,
    string Kid,
    string Alg,
    string N,
    string E);
