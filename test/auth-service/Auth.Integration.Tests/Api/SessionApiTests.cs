namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Linha de base (spec 2026092512): login, refresh com rotacao e logout pela API contra o banco real.
[Collection(AuthApiCollection.Name)]
public sealed class SessionApiTests : IAsyncLifetime
{
    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public SessionApiTests(AuthApiFixture fixture)
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
    public async Task ShouldReturnBearerTokensAndStoreOnlyTheRefreshTokenHashWhenLoginSucceeds()
    {
        var user = await _api.CreateUserAsync("login.ok");

        var response = await _api.PostAsync("/api/auth/login", new { login = "login.ok", password = TestApi.Password });
        var tokens = await TestApi.ReadTokensAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(tokens.AccessToken);
        Assert.Equal(1, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
        Assert.Equal(0, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.refresh_tokens WHERE token_hash = @Raw;", new { Raw = tokens.RefreshToken }));
    }

    [Fact]
    public async Task ShouldReturnUnauthorizedWithTheSameBodyForWrongPasswordAndUnknownLogin()
    {
        await _api.CreateUserAsync("login.wrong");

        var wrongPassword = await _api.PostAsync("/api/auth/login", new { login = "login.wrong", password = "Wrong-Password-123" });
        var unknownLogin = await _api.PostAsync("/api/auth/login", new { login = "nobody", password = "Wrong-Password-123" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownLogin.StatusCode);
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(), await unknownLogin.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ShouldReturnBadRequestWhenUserIsNotActive()
    {
        await _api.CreateUserAsync("login.inactive", confirmed: false);

        var response = await _api.PostAsync("/api/auth/login", new { login = "login.inactive", password = TestApi.Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ShouldKeepBothSessionsValidWhenUserLogsInTwice()
    {
        var user = await _api.CreateUserAsync("login.twice");

        var first = await _api.LoginAsync("login.twice");
        var second = await _api.LoginAsync("login.twice");
        var refreshWithFirst = await _api.PostAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        var refreshWithSecond = await _api.PostAsync("/api/auth/refresh", new { refreshToken = second.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refreshWithFirst.StatusCode);
        Assert.Equal(HttpStatusCode.OK, refreshWithSecond.StatusCode);
        Assert.Equal(2, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
    }

    [Fact]
    public async Task ShouldRotateRefreshTokenAndRejectTheOldOne()
    {
        var user = await _api.CreateUserAsync("refresh.rotation");
        var session = await _api.LoginAsync("refresh.rotation");

        var refreshed = await _api.PostAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });
        var rotated = await TestApi.ReadTokensAsync(refreshed);
        var withNew = await _api.PostAsync("/api/auth/refresh", new { refreshToken = rotated.RefreshToken });
        var reused = await _api.PostAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.NotEqual(session.RefreshToken, rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, withNew.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

        // Reusar o token antigo e sinal de copia: a sessao inteira cai (spec 2026092507).
        Assert.Equal(0, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown-refresh-token")]
    public async Task ShouldReturnUnauthorizedWhenRefreshTokenIsEmptyOrUnknown(string refreshToken)
    {
        var response = await _api.PostAsync("/api/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnUnauthorizedWhenRefreshTokenIsExpired()
    {
        await _api.CreateUserAsync("refresh.expired");
        var session = await _api.LoginAsync("refresh.expired");
        await _api.ExpireRefreshTokenAsync(session.RefreshToken);

        var response = await _api.PostAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldRejectRefreshWhenUserWasDeleted()
    {
        var user = await _api.CreateUserAsync("refresh.deleted");
        var session = await _api.LoginAsync("refresh.deleted");
        await _api.ExecuteAsync("UPDATE auth.users SET deleted_at = SYSDATETIMEOFFSET(), active = 0 WHERE external_id = @Id;", new { Id = user.ExternalId });

        var response = await _api.PostAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldRevokeRefreshTokenOnLogoutAndBeIdempotent()
    {
        var user = await _api.CreateUserAsync("logout.user");
        var session = await _api.LoginAsync("logout.user");

        var first = await _api.PostAsync("/api/auth/logout", new { refreshToken = session.RefreshToken });
        var second = await _api.PostAsync("/api/auth/logout", new { refreshToken = session.RefreshToken });
        var refreshAfter = await _api.PostAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfter.StatusCode);
        Assert.Equal(0, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown-refresh-token")]
    public async Task ShouldReturnUnauthorizedOnLogoutWhenRefreshTokenIsEmptyOrUnknown(string refreshToken)
    {
        var response = await _api.PostAsync("/api/auth/logout", new { refreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
