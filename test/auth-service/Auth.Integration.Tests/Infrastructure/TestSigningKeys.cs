namespace Ouroboros.Auth.Integration.Tests.Infrastructure;

using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

// Chaves RSA so de teste, geradas uma vez por execucao e gravadas em PEM numa pasta temporaria (a API le o PEM por
// caminho, como em producao). O teste usa a privada pra forjar tokens (expirado, outra chave, outro emissor).
public static class TestSigningKeys
{
    public const string ActiveKid = "test-key-1";
    public const string PublishedKid = "test-key-2";

    public const string Issuer = "http://localhost:8082";
    public const string Audience = "ouroboros";

    private static readonly Lazy<string> Directory = new(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), $"ouroboros-auth-test-keys-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(path);
        return path;
    });

    private static readonly Lazy<RSA> Active = new(() => Generate(ActiveKid));
    private static readonly Lazy<RSA> Published = new(() => Generate(PublishedKid));

    public static string PemPath(string kid) => Path.Combine(Directory.Value, $"{kid}.pem");

    // Credencial da chave Active: assina como o auth-service assina.
    public static SigningCredentials ActiveCredentials => Credentials(Active.Value, ActiveKid);

    // Credencial da chave Published (o auth-service a aceita na validacao, mas nao assina com ela).
    public static SigningCredentials PublishedCredentials => Credentials(Published.Value, PublishedKid);

    public static RsaSecurityKey PublicKey(string kid) => new((kid == ActiveKid ? Active : Published).Value.ExportParameters(false)) { KeyId = kid };

    public static void EnsureFilesExist()
    {
        _ = Active.Value;
        _ = Published.Value;
    }

    // PEM de uma chave RSA nova (de qualquer tamanho), pra testar a validacao das chaves.
    public static string WritePem(
        string name,
        int keySizeInBits)
    {
        using var rsa = RSA.Create(keySizeInBits);
        var path = Path.Combine(Directory.Value, $"{name}.pem");
        File.WriteAllText(path, rsa.ExportPkcs8PrivateKeyPem());

        return path;
    }

    private static RSA Generate(string kid)
    {
        var rsa = RSA.Create(2048);
        File.WriteAllText(PemPath(kid), rsa.ExportPkcs8PrivateKeyPem());

        return rsa;
    }

    private static SigningCredentials Credentials(
        RSA rsa,
        string kid) =>
        new(new RsaSecurityKey(rsa) { KeyId = kid }, SecurityAlgorithms.RsaSha256);
}
