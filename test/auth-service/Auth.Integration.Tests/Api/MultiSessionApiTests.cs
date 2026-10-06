namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using System.Text;
using System.Text.Json;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092506: varias sessoes simultaneas, claim sid, last_login_at, login atomico e logout-all.
[Collection(AuthApiCollection.Name)]
public sealed class MultiSessionApiTests : IAsyncLifetime
{
    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public MultiSessionApiTests(AuthApiFixture fixture)
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
    public async Task ShouldCreateOneSessionPerLoginAndPutItsIdInTheSidClaim()
    {
        var user = await _api.CreateUserAsync("multi.sid");

        var first = await _api.LoginAsync("multi.sid");
        var second = await _api.LoginAsync("multi.sid");

        var sessions = await SessionIdsAsync(user.ExternalId);
        Assert.Equal(2, sessions.Count);
        Assert.Equal(2, sessions.Distinct().Count());
        Assert.Contains(Guid.Parse(ReadSid(first.AccessToken)), sessions);
        Assert.Contains(Guid.Parse(ReadSid(second.AccessToken)), sessions);
        Assert.NotEqual(ReadSid(first.AccessToken), ReadSid(second.AccessToken));
    }

    [Fact]
    public async Task ShouldKeepTheSessionIdAcrossRefreshRotation()
    {
        var user = await _api.CreateUserAsync("multi.rotate");
        var login = await _api.LoginAsync("multi.rotate");

        var rotated = await TestApi.ReadTokensAsync(await _api.PostAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken }));

        Assert.Equal(ReadSid(login.AccessToken), ReadSid(rotated.AccessToken));
        Assert.Single((await SessionIdsAsync(user.ExternalId)).Distinct());
    }

    [Fact]
    public async Task ShouldRecordLastLoginAtAndResetFailureCountWhenLoginSucceeds()
    {
        var user = await _api.CreateUserAsync("multi.lastlogin");
        await _api.PostAsync("/api/auth/login", new { login = "multi.lastlogin", password = "Wrong-Password-123" });
        var before = await _api.QueryAsync<int>("SELECT access_failed_count FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId });

        await _api.LoginAsync("multi.lastlogin");

        Assert.Equal(1, before);
        Assert.Equal(0, await _api.QueryAsync<int>("SELECT access_failed_count FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }));
        Assert.NotNull(await _api.QueryAsync<DateTimeOffset?>("SELECT last_login_at FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }));
    }

    [Fact]
    public async Task ShouldNotRecordLastLoginAtWhenLoginIsRejected()
    {
        var user = await _api.CreateUserAsync("multi.rejected");

        await _api.PostAsync("/api/auth/login", new { login = "multi.rejected", password = "Wrong-Password-123" });

        Assert.Null(await _api.QueryAsync<DateTimeOffset?>("SELECT last_login_at FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }));
    }

    [Theory]
    [InlineData(FaultTiming.Before)]
    [InlineData(FaultTiming.After)]
    public async Task ShouldCreateNoRefreshTokenAndKeepUserUntouchedWhenUserUpdateFails(FaultTiming timing)
    {
        var user = await _api.CreateUserAsync("multi.fault");
        await _api.PostAsync("/api/auth/login", new { login = "multi.fault", password = "Wrong-Password-123" });

        using var factory = _fixture.CreateFactoryFailingOn<IUserRepository>(nameof(IUserRepository.TryRegisterLoginAsync), timing);
        using var failing = new TestApi(_fixture, factory.CreateClient());

        var response = await failing.PostAsync("/api/auth/login", new { login = "multi.fault", password = TestApi.Password });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.refresh_tokens;"));
        Assert.Null(await _api.QueryAsync<DateTimeOffset?>("SELECT last_login_at FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }));
        Assert.Equal(1, await _api.QueryAsync<int>("SELECT access_failed_count FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }));
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsync("/api/auth/login", new { login = "multi.fault", password = TestApi.Password })).StatusCode);
    }

    [Fact]
    public async Task ShouldRollBackLastLoginAtWhenRefreshTokenInsertFails()
    {
        var user = await _api.CreateUserAsync("multi.insert");

        using var factory = _fixture.CreateFactoryFailingOn<IRefreshTokenRepository>(nameof(IRefreshTokenRepository.AddAsync));
        using var failing = new TestApi(_fixture, factory.CreateClient());

        var response = await failing.PostAsync("/api/auth/login", new { login = "multi.insert", password = TestApi.Password });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.refresh_tokens;"));
        Assert.Null(await _api.QueryAsync<DateTimeOffset?>("SELECT last_login_at FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }));
    }

    [Fact]
    public async Task ShouldEndOnlyTheSessionOfTheLoggedOutRefreshToken()
    {
        var user = await _api.CreateUserAsync("multi.logout");
        var first = await _api.LoginAsync("multi.logout");
        var second = await _api.LoginAsync("multi.logout");

        var logout = await _api.PostAsync("/api/auth/logout", new { refreshToken = first.RefreshToken });
        var refreshFirst = await _api.PostAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken });
        var refreshSecond = await _api.PostAsync("/api/auth/refresh", new { refreshToken = second.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshFirst.StatusCode);
        Assert.Equal(HttpStatusCode.OK, refreshSecond.StatusCode);
        Assert.Equal(1, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
    }

    [Fact]
    public async Task ShouldEndEverySessionOfTheUserOnLogoutAllAndKeepOtherUsersSessions()
    {
        var user = await _api.CreateUserAsync("multi.all");
        var other = await _api.CreateUserAsync("multi.other");
        var first = await _api.LoginAsync("multi.all");
        var second = await _api.LoginAsync("multi.all");
        var otherSession = await _api.LoginAsync("multi.other");

        var response = await _api.PostAsync("/api/auth/logout-all", null, first.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.PostAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.PostAsync("/api/auth/refresh", new { refreshToken = second.RefreshToken })).StatusCode);
        Assert.Equal(1, await _api.CountActiveRefreshTokensAsync(other.ExternalId));
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsync("/api/auth/refresh", new { refreshToken = otherSession.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task ShouldKeepAccessTokenAlreadyIssuedValidAfterLogoutAll()
    {
        await _api.CreateUserAsync("multi.limit");
        var session = await _api.LoginAsync("multi.limit");

        await _api.PostAsync("/api/auth/logout-all", null, session.AccessToken);
        var query = await _api.PostAsync("/api/users/search", new { login = "multi.limit" }, session.AccessToken);

        // Limitacao documentada: o access token vale ate expirar (no maximo 15 min).
        Assert.Equal(HttpStatusCode.OK, query.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnUnauthorizedOnLogoutAllWithoutAccessToken()
    {
        var response = await _api.PostAsync("/api/auth/logout-all", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldAcceptLogoutAllTwiceBecauseItIsIdempotent()
    {
        await _api.CreateUserAsync("multi.idem");
        var session = await _api.LoginAsync("multi.idem");

        var first = await _api.PostAsync("/api/auth/logout-all", null, session.AccessToken);
        var second = await _api.PostAsync("/api/auth/logout-all", null, session.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task ShouldNotExposeLastLoginAtOrSessionIdInTheLoginBody()
    {
        await _api.CreateUserAsync("multi.body");

        var response = await _api.PostAsync("/api/auth/login", new { login = "multi.body", password = TestApi.Password });
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("lastLogin", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionId", body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<Guid>> SessionIdsAsync(Guid userExternalId)
    {
        const string sql = """
            SELECT refreshTokens.session_id
            FROM auth.refresh_tokens AS refreshTokens
            INNER JOIN auth.users AS users ON users.id = refreshTokens.user_id
            WHERE users.external_id = @Id;
            """;

        return await _api.QueryListAsync<Guid>(sql, new { Id = userExternalId });
    }

    private static string ReadSid(string accessToken)
    {
        var payload = accessToken.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));

        return document.RootElement.GetProperty("sid").GetString()!;
    }
}
