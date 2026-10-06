namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092518: rotacao de chaves sem queda (runbook) e validacao das chaves no startup, com a API de verdade.
[Collection(AuthApiCollection.Name)]
public sealed class JwtKeyRotationApiTests : IAsyncLifetime
{
    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public JwtKeyRotationApiTests(AuthApiFixture fixture)
    {
        _fixture = fixture;
        _api = new TestApi(fixture);
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ShouldRotateTheKeyWithoutDroppingAnyValidToken()
    {
        await _api.CreateUserAsync("rotation.user");
        var oldKey = ("rotation-old", TestSigningKeys.WritePem("rotation-old", 2048));
        var newKey = ("rotation-new", TestSigningKeys.WritePem("rotation-new", 2048));

        // Estado inicial: so a chave antiga, Active.
        var oldToken = await Run([(oldKey.Item1, oldKey.Item2, "Active")], async client =>
        {
            Assert.Equal(["rotation-old"], await KidsInJwksAsync(client));

            return (await client.LoginAsync("rotation.user")).AccessToken;
        });

        // Passo 1: publica a chave nova como Published. A antiga continua assinando.
        await Run([(oldKey.Item1, oldKey.Item2, "Active"), (newKey.Item1, newKey.Item2, "Published")], async client =>
        {
            Assert.Equal(["rotation-old", "rotation-new"], await KidsInJwksAsync(client));
            Assert.Equal("rotation-old", new JsonWebToken((await client.LoginAsync("rotation.user")).AccessToken).Kid);
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, oldToken));
        });

        // Passo 3 (depois de esperar o cache de 1 h do JWKS): a nova vira Active e a antiga fica Published.
        var newToken = await Run([(newKey.Item1, newKey.Item2, "Active"), (oldKey.Item1, oldKey.Item2, "Published")], async client =>
        {
            var token = (await client.LoginAsync("rotation.user")).AccessToken;

            Assert.Equal("rotation-new", new JsonWebToken(token).Kid);
            Assert.Equal(["rotation-new", "rotation-old"], await KidsInJwksAsync(client));
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, oldToken));
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, token));

            return token;
        });

        // Passo 4 (depois de 15 min mais o cache): a antiga sai da lista.
        await Run([(newKey.Item1, newKey.Item2, "Active")], async client =>
        {
            Assert.Equal(["rotation-new"], await KidsInJwksAsync(client));
            Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(client, oldToken));
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, newToken));
        });
    }

    [Fact]
    public async Task ShouldKeepRefreshTokensValidThroughARotationBecauseTheyAreOpaque()
    {
        await _api.CreateUserAsync("rotation.refresh");
        var oldKey = ("refresh-old", TestSigningKeys.WritePem("refresh-old", 2048));
        var newKey = ("refresh-new", TestSigningKeys.WritePem("refresh-new", 2048));

        var refreshToken = await Run([(oldKey.Item1, oldKey.Item2, "Active")], async client => (await client.LoginAsync("rotation.refresh")).RefreshToken);

        await Run([(newKey.Item1, newKey.Item2, "Active")], async client =>
        {
            var refreshed = await client.PostAsync("/api/auth/refresh", new { refreshToken });
            var tokens = await TestApi.ReadTokensAsync(refreshed);

            Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
            Assert.Equal("refresh-new", new JsonWebToken(tokens.AccessToken).Kid);
        });
    }

    [Fact]
    public void ShouldFailAtStartupWithoutAnyKey()
    {
        using var factory = new AuthApiFactory(_fixture.ConnectionString, []);

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("at least one key", error.Message);
    }

    [Fact]
    public void ShouldFailAtStartupWithoutAnActiveKey()
    {
        using var factory = new AuthApiFactory(_fixture.ConnectionString, [("only-published", TestSigningKeys.PemPath(TestSigningKeys.PublishedKid), "Published")]);

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("exactly one Active key, found 0", error.Message);
    }

    [Fact]
    public void ShouldFailAtStartupWithTwoActiveKeys()
    {
        using var factory = new AuthApiFactory(
            _fixture.ConnectionString,
            [
                ("active-1", TestSigningKeys.PemPath(TestSigningKeys.ActiveKid), "Active"),
                ("active-2", TestSigningKeys.PemPath(TestSigningKeys.PublishedKid), "Active"),
            ]);

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("exactly one Active key, found 2", error.Message);
    }

    [Fact]
    public void ShouldFailAtStartupWithAKeySmallerThan2048Bits()
    {
        using var factory = new AuthApiFactory(_fixture.ConnectionString, [("weak", TestSigningKeys.WritePem("startup-weak", 1024), "Active")]);

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("1024-bit", error.Message);
    }

    [Fact]
    public void ShouldFailAtStartupWithARepeatedKid()
    {
        using var factory = new AuthApiFactory(
            _fixture.ConnectionString,
            [
                ("same", TestSigningKeys.PemPath(TestSigningKeys.ActiveKid), "Active"),
                ("same", TestSigningKeys.PemPath(TestSigningKeys.PublishedKid), "Published"),
            ]);

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("repeated Kid 'same'", error.Message);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://auth.example.com")]
    public void ShouldFailAtStartupWhenTheIssuerIsNotAPublicHttpUrl(string issuer)
    {
        using var factory = _fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("Jwt:Issuer", issuer));

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Jwt:Issuer", error.Message);
    }

    private async Task Run(
        IReadOnlyList<(string Kid, string Path, string Status)> keys,
        Func<TestApi, Task> act)
    {
        await Run(keys, async client =>
        {
            await act(client);

            return 0;
        });
    }

    private async Task<T> Run<T>(
        IReadOnlyList<(string Kid, string Path, string Status)> keys,
        Func<TestApi, Task<T>> act)
    {
        using var factory = new AuthApiFactory(_fixture.ConnectionString, keys);
        using var client = new TestApi(_fixture, factory.CreateClient());

        return await act(client);
    }

    private static async Task<HttpStatusCode> StatusAsync(
        TestApi client,
        string accessToken)
    {
        // Qualquer rota protegida serve: 401 e so a validacao do token falhando.
        return (await client.PostAsync("/api/users/search", new { login = "rotation.user" }, accessToken)).StatusCode;
    }

    private static async Task<string[]> KidsInJwksAsync(TestApi client)
    {
        var response = await client.GetAsync("/.well-known/jwks.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("keys").EnumerateArray().Select(key => key.GetProperty("kid").GetString()!).ToArray();
    }
}
