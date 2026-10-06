namespace Ouroboros.Auth.Integration.Tests.Security;

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Infrastructure.Security;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092518: validacao das chaves no startup e emissao RS256 com kid.
public sealed class SigningKeyStoreTests
{
    private static SigningKeySettings Key(
        string kid,
        string path,
        SigningKeyStatus status) =>
        new() { Kid = kid, PrivateKeyPath = path, Status = status };

    private static SigningKeySettings ActiveKey()
    {
        TestSigningKeys.EnsureFilesExist();

        return Key(TestSigningKeys.ActiveKid, TestSigningKeys.PemPath(TestSigningKeys.ActiveKid), SigningKeyStatus.Active);
    }

    private static SigningKeySettings PublishedKey()
    {
        TestSigningKeys.EnsureFilesExist();

        return Key(TestSigningKeys.PublishedKid, TestSigningKeys.PemPath(TestSigningKeys.PublishedKid), SigningKeyStatus.Published);
    }

    [Fact]
    public void ShouldAcceptOneActiveKeyAndPublishedKeys()
    {
        Assert.Empty(SigningKeyStore.Validate([ActiveKey()]));
        Assert.Empty(SigningKeyStore.Validate([ActiveKey(), PublishedKey()]));
    }

    [Fact]
    public void ShouldFailWithoutAnyKey()
    {
        var errors = SigningKeyStore.Validate([]);

        Assert.Contains(errors, error => error.Contains("at least one key"));
        Assert.Throws<InvalidOperationException>(() => SigningKeyStore.Load([]));
    }

    [Fact]
    public void ShouldFailWithoutAnActiveKey()
    {
        var errors = SigningKeyStore.Validate([PublishedKey()]);

        Assert.Contains(errors, error => error.Contains("exactly one Active key, found 0"));
        Assert.Throws<InvalidOperationException>(() => SigningKeyStore.Load([PublishedKey()]));
    }

    [Fact]
    public void ShouldFailWithTwoActiveKeys()
    {
        var second = Key("another-active", TestSigningKeys.PemPath(TestSigningKeys.PublishedKid), SigningKeyStatus.Active);

        var errors = SigningKeyStore.Validate([ActiveKey(), second]);

        Assert.Contains(errors, error => error.Contains("exactly one Active key, found 2"));
        Assert.Throws<InvalidOperationException>(() => SigningKeyStore.Load([ActiveKey(), second]));
    }

    [Fact]
    public void ShouldFailWithAnRsaKeySmallerThan2048Bits()
    {
        var weak = Key("weak-1024", TestSigningKeys.WritePem("weak-1024", 1024), SigningKeyStatus.Published);

        var errors = SigningKeyStore.Validate([ActiveKey(), weak]);

        Assert.Contains(errors, error => error.Contains("weak-1024") && error.Contains("1024-bit") && error.Contains("2048"));
    }

    [Fact]
    public void ShouldFailWhenTheActiveKeyIsSmallerThan2048Bits()
    {
        var weak = Key("weak-active", TestSigningKeys.WritePem("weak-active", 1024), SigningKeyStatus.Active);

        Assert.Contains(SigningKeyStore.Validate([weak]), error => error.Contains("weak-active"));
        Assert.Throws<InvalidOperationException>(() => SigningKeyStore.Load([weak]));
    }

    [Fact]
    public void ShouldFailWithARepeatedKid()
    {
        var repeated = Key(TestSigningKeys.ActiveKid, TestSigningKeys.PemPath(TestSigningKeys.PublishedKid), SigningKeyStatus.Published);

        var errors = SigningKeyStore.Validate([ActiveKey(), repeated]);

        Assert.Contains(errors, error => error.Contains("repeated Kid") && error.Contains(TestSigningKeys.ActiveKid));
    }

    [Fact]
    public void ShouldFailWhenTheKidIsMissing()
    {
        var withoutKid = Key(string.Empty, TestSigningKeys.PemPath(TestSigningKeys.ActiveKid), SigningKeyStatus.Active);

        Assert.Contains(SigningKeyStore.Validate([withoutKid]), error => error.Contains("needs a Kid"));
    }

    [Fact]
    public void ShouldFailWhenThePemFileDoesNotExist()
    {
        var missing = Key("missing", Path.Combine(Path.GetTempPath(), "does-not-exist.pem"), SigningKeyStatus.Active);

        Assert.Contains(SigningKeyStore.Validate([missing]), error => error.Contains("missing") && error.Contains("PrivateKeyPath"));
    }

    [Fact]
    public void ShouldFailWhenTheFileIsNotAnRsaPrivateKey()
    {
        var path = Path.Combine(Path.GetTempPath(), $"not-a-key-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, "this is not a pem file");

        var errors = SigningKeyStore.Validate([Key("garbage", path, SigningKeyStatus.Active)]);

        Assert.Contains(errors, error => error.Contains("garbage") && error.Contains("valid RSA private key"));
    }

    [Fact]
    public void ShouldNeverCarryPrivateParametersInTheJsonWebKeys()
    {
        var store = SigningKeyStore.Load([ActiveKey(), PublishedKey()]);

        var json = JsonSerializer.Serialize(store.JsonWebKeys);

        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.EnumerateArray().SelectMany(key => key.EnumerateObject().Select(property => property.Name)).Distinct().Order();

        Assert.Equal(["Alg", "E", "Kid", "Kty", "N", "Use"], properties);
        foreach (var privateParameter in new[] { "\"d\"", "\"p\"", "\"q\"", "\"dp\"", "\"dq\"", "\"qi\"", "\"D\"", "\"P\"", "\"Q\"", "\"DP\"", "\"DQ\"", "\"InverseQ\"" })
        {
            Assert.DoesNotContain(privateParameter, json);
        }

        Assert.All(store.PublicKeys, key => Assert.False(key.PrivateKeyStatus == PrivateKeyStatus.Exists));
    }

    [Fact]
    public void ShouldSignWithTheActiveKeyAndPublishTheActiveAndThePublishedOnes()
    {
        var store = SigningKeyStore.Load([PublishedKey(), ActiveKey()]);

        Assert.Equal(TestSigningKeys.ActiveKid, store.ActiveCredentials.Key.KeyId);
        Assert.Equal(SecurityAlgorithms.RsaSha256, store.ActiveCredentials.Algorithm);
        Assert.Equal([TestSigningKeys.PublishedKid, TestSigningKeys.ActiveKid], store.PublicKeys.Select(key => key.KeyId));
        Assert.Equal([TestSigningKeys.PublishedKid, TestSigningKeys.ActiveKid], store.JsonWebKeys.Select(key => key.Kid));
    }

    [Fact]
    public async Task ShouldIssueRs256TokenWithTheKidOfTheActiveKeyAndTheSessionId()
    {
        var store = SigningKeyStore.Load([PublishedKey(), ActiveKey()]);
        var settings = new JwtSettings { Issuer = TestSigningKeys.Issuer, Audience = TestSigningKeys.Audience, AccessTokenExpirationMinutes = 15 };
        var sessionId = Guid.NewGuid();

        var token = new JwtTokenGenerator(settings, store).Generate(Guid.NewGuid(), "jdoe", "jdoe@example.com", UserRole.User, sessionId);

        var parsed = new JsonWebToken(token.Value);
        Assert.Equal("RS256", parsed.Alg);
        Assert.Equal(TestSigningKeys.ActiveKid, parsed.Kid);
        Assert.Equal(sessionId.ToString(), parsed.GetClaim("sid").Value);
        Assert.Equal(TestSigningKeys.Issuer, parsed.Issuer);

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = TestSigningKeys.Issuer,
            ValidAudience = TestSigningKeys.Audience,
            IssuerSigningKey = TestSigningKeys.PublicKey(TestSigningKeys.ActiveKid),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        };

        Assert.True((await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, parameters)).IsValid);

        // A chave Published nao assinou: com a publica dela o token nao valida.
        parameters.IssuerSigningKey = TestSigningKeys.PublicKey(TestSigningKeys.PublishedKid);
        Assert.False((await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, parameters)).IsValid);
    }
}
