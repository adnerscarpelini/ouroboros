namespace Ouroboros.Auth.Integration.Tests.Security;

using System.Diagnostics;
using System.Security.Cryptography;
using Ouroboros.Auth.Infrastructure.Security;
using Xunit;
using Xunit.Abstractions;

// Spec 2026092509: custo do hash, normalizacao NFKC e re-hash.
public sealed class PasswordHasherTests
{
    private readonly ITestOutputHelper _output;
    private readonly Pbkdf2PasswordHasher _hasher = new();

    public PasswordHasherTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ShouldHashWithSixHundredThousandIterations()
    {
        var hash = _hasher.Hash("Str0ng-Passphrase-1");

        Assert.StartsWith("600000.", hash);
        Assert.Equal(3, hash.Split('.').Length);
        Assert.True(_hasher.Verify("Str0ng-Passphrase-1", hash));
        Assert.False(_hasher.NeedsRehash(hash));
    }

    [Fact]
    public void ShouldVerifyAndFlagRehashWhenHashWasMadeWithOneHundredThousandIterations()
    {
        var oldHash = HashWithIterations("Str0ng-Passphrase-1", 100_000);

        Assert.True(_hasher.Verify("Str0ng-Passphrase-1", oldHash));
        Assert.False(_hasher.Verify("Another-Passphrase-2", oldHash));
        Assert.True(_hasher.NeedsRehash(oldHash));
    }

    [Fact]
    public void ShouldFlagRehashWhenHashIsMalformed()
    {
        Assert.True(_hasher.NeedsRehash("not-a-hash"));
    }

    [Fact]
    public void ShouldNormalizeWithNfkcBeforeHashingAndVerifying()
    {
        // Composta (U+00E9) e decomposta (e + U+0301) sao a mesma senha depois do NFKC; a ligadura U+FB01 vira "fi".
        var composed = "café-passphrase-ﬁx";
        var decomposed = "café-passphrase-fix";

        var hash = _hasher.Hash(composed);

        Assert.True(_hasher.Verify(decomposed, hash));
        Assert.True(_hasher.Verify(composed, _hasher.Hash(decomposed)));
    }

    [Fact]
    public void ShouldUseTheSameIterationsInTheDummyHash()
    {
        Assert.StartsWith("600000.", _hasher.DummyHash);
        Assert.False(_hasher.NeedsRehash(_hasher.DummyHash));
    }

    [Fact]
    public void ShouldMeasureVerificationTimeWithSixHundredThousandIterations()
    {
        var hash = _hasher.Hash("Str0ng-Passphrase-1");
        _hasher.Verify("Str0ng-Passphrase-1", hash);

        var stopwatch = Stopwatch.StartNew();
        const int runs = 5;

        for (var run = 0; run < runs; run++)
        {
            _hasher.Verify("Str0ng-Passphrase-1", hash);
        }

        stopwatch.Stop();
        var average = stopwatch.Elapsed.TotalMilliseconds / runs;

        _output.WriteLine($"PBKDF2 600000 iteracoes: media de {average:F0} ms por verificacao ({runs} execucoes).");

        // Referencia da spec: abaixo de 500 ms. O limite do teste e folgado pra nao falhar em maquina carregada.
        Assert.True(average < 2000, $"Verificacao levou {average:F0} ms em media.");
    }

    internal static string HashWithIterations(string password, int iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);

        return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}
