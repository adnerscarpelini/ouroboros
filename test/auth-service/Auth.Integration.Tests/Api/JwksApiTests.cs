namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092518: RS256, JWKS, descoberta e validacao pelos consumidores.
[Collection(AuthApiCollection.Name)]
public sealed class JwksApiTests : IAsyncLifetime
{
    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public JwksApiTests(AuthApiFixture fixture)
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
    public async Task ShouldIssueRs256TokensWithTheKidOfTheActiveKeyAndValidateThem()
    {
        var user = await _api.CreateUserAsync("jwks.login");
        var session = await _api.LoginAsync("jwks.login");

        var token = new JsonWebToken(session.AccessToken);
        var query = await _api.GetAsync($"/api/users/{user.ExternalId}", session.AccessToken);

        Assert.Equal("RS256", token.Alg);
        Assert.Equal(TestSigningKeys.ActiveKid, token.Kid);
        Assert.Equal(TestSigningKeys.Issuer, token.Issuer);
        Assert.Equal(HttpStatusCode.OK, query.StatusCode);
    }

    [Fact]
    public async Task ShouldPublishTheActiveAndThePublishedKeysWithoutPrivateParameters()
    {
        var response = await _api.GetAsync("/.well-known/jwks.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(body);
        var keys = document.RootElement.GetProperty("keys").EnumerateArray().ToList();

        Assert.Equal([TestSigningKeys.ActiveKid, TestSigningKeys.PublishedKid], keys.Select(key => key.GetProperty("kid").GetString()));
        Assert.All(keys, key =>
        {
            Assert.Equal("RSA", key.GetProperty("kty").GetString());
            Assert.Equal("sig", key.GetProperty("use").GetString());
            Assert.Equal("RS256", key.GetProperty("alg").GetString());
            Assert.Equal(["alg", "e", "kid", "kty", "n", "use"], key.EnumerateObject().Select(property => property.Name).Order());
        });
    }

    [Fact]
    public async Task ShouldNeverExposePrivateParametersInTheJwks()
    {
        var response = await _api.GetAsync("/.well-known/jwks.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        foreach (var key in document.RootElement.GetProperty("keys").EnumerateArray())
        {
            foreach (var privateParameter in new[] { "d", "p", "q", "dp", "dq", "qi", "oth" })
            {
                Assert.False(key.TryGetProperty(privateParameter, out _), $"o JWKS expoe o parametro privado '{privateParameter}'");
            }
        }
    }

    [Fact]
    public async Task ShouldPublishRealPublicKeysThatVerifyTheTokens()
    {
        await _api.CreateUserAsync("jwks.verify");
        var session = await _api.LoginAsync("jwks.verify");
        var response = await _api.GetAsync("/.well-known/jwks.json");
        var keys = new JsonWebKeySet(await response.Content.ReadAsStringAsync());

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(session.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = TestSigningKeys.Issuer,
            ValidAudience = TestSigningKeys.Audience,
            IssuerSigningKeys = keys.GetSigningKeys(),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("/.well-known/jwks.json")]
    [InlineData("/.well-known/openid-configuration")]
    public async Task ShouldBePublicCacheableAndOutsideTheRateLimit(string path)
    {
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;

        // Os limites dos testes sao de 3 requisicoes por IP: os endpoints publicos nao entram em nenhuma politica.
        for (var request = 0; request < 12; request++)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, path);
            message.Headers.Add(AuthApiFactory.RemoteIpHeader, "10.8.8.8");
            last = await _fixture.CreateClient().SendAsync(message);
            statuses.Add(last.StatusCode);
        }

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal("public, max-age=3600", last!.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task ShouldReturnAMinimalDiscoveryDocumentWithIssuerAndJwksUri()
    {
        var response = await _api.GetAsync("/.well-known/openid-configuration");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["issuer", "jwks_uri"], document.RootElement.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(TestSigningKeys.Issuer, document.RootElement.GetProperty("issuer").GetString());
        Assert.Equal($"{TestSigningKeys.Issuer}/.well-known/jwks.json", document.RootElement.GetProperty("jwks_uri").GetString());
    }

    [Fact]
    public async Task ShouldLetAConsumerConfiguredOnlyWithTheAuthorityValidateTheTokenThroughDiscovery()
    {
        await _api.CreateUserAsync("jwks.consumer");
        var session = await _api.LoginAsync("jwks.consumer");

        // Faz o que o JwtBearer de um consumidor com so a Authority faz: busca o documento de descoberta, depois o JWKS.
        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{TestSigningKeys.Issuer}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new TestServerDocumentRetriever(_fixture.CreateClient()));
        var configuration = await manager.GetConfigurationAsync(CancellationToken.None);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(session.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = configuration.Issuer,
            ValidAudience = TestSigningKeys.Audience,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        });

        Assert.Equal(TestSigningKeys.Issuer, configuration.Issuer);
        Assert.Equal(2, configuration.SigningKeys.Count);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ShouldRejectATokenSignedWithHs256UsingThePublicKeyAsSecret()
    {
        var user = await _api.CreateUserAsync("jwks.confusion");
        var publicKey = TestSigningKeys.PublicKey(TestSigningKeys.ActiveKid).Parameters.Modulus!;
        var forged = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = TestSigningKeys.Issuer,
            Audience = TestSigningKeys.Audience,
            Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = user.ExternalId.ToString() },
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(publicKey) { KeyId = TestSigningKeys.ActiveKid }, SecurityAlgorithms.HmacSha256),
        });

        var response = await _api.GetAsync($"/api/users/{user.ExternalId}", forged);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldRejectATokenSignedWithTheOldSharedHmacKey()
    {
        var user = await _api.CreateUserAsync("jwks.hs256");
        var forged = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = TestSigningKeys.Issuer,
            Audience = TestSigningKeys.Audience,
            Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = user.ExternalId.ToString() },
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-tests-signing-key-with-32-bytes-or-more")), SecurityAlgorithms.HmacSha256),
        });

        var response = await _api.GetAsync($"/api/users/{user.ExternalId}", forged);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldAcceptATokenSignedWithThePublishedKeyAndRejectOneWithAnUnknownKid()
    {
        var user = await _api.CreateUserAsync("jwks.published");

        var withPublished = Token(user, TestSigningKeys.PublishedCredentials, TestSigningKeys.Issuer);
        var unknown = new SigningCredentials(new RsaSecurityKey(RSA.Create(2048)) { KeyId = "unknown-key" }, SecurityAlgorithms.RsaSha256);
        var withUnknown = Token(user, unknown, TestSigningKeys.Issuer);

        Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync($"/api/users/{user.ExternalId}", withPublished)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.GetAsync($"/api/users/{user.ExternalId}", withUnknown)).StatusCode);
    }

    [Fact]
    public async Task ShouldRejectATokenFromTheOldIssuerEvenWhenItIsSignedWithTheActiveKey()
    {
        var user = await _api.CreateUserAsync("jwks.issuer");
        var oldIssuer = Token(user, TestSigningKeys.ActiveCredentials, "ouroboros-auth");

        var response = await _api.GetAsync($"/api/users/{user.ExternalId}", oldIssuer);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldKeepRefreshTokensValidAfterTheSwitchBecauseTheyAreOpaque()
    {
        await _api.CreateUserAsync("jwks.refresh");
        var session = await _api.LoginAsync("jwks.refresh");

        var refreshed = await _api.PostAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(TestSigningKeys.ActiveKid, new JsonWebToken((await TestApi.ReadTokensAsync(refreshed)).AccessToken).Kid);
    }

    private static string Token(
        User user,
        SigningCredentials credentials,
        string issuer) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = TestSigningKeys.Audience,
            Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = user.ExternalId.ToString() },
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = credentials,
        });

    // O issuer e a URL publica (http://localhost:8082), mas o TestServer so atende pelo caminho: reescreve o host.
    private sealed class TestServerDocumentRetriever : IDocumentRetriever
    {
        private readonly HttpClient _client;

        public TestServerDocumentRetriever(HttpClient client)
        {
            _client = client;
        }

        public Task<string> GetDocumentAsync(
            string address,
            CancellationToken cancel) => _client.GetStringAsync(new Uri(address).PathAndQuery, cancel);
    }
}
