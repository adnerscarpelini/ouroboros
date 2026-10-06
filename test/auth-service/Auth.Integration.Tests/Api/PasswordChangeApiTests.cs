namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092517: troca de senha autenticada, pela API contra o banco real.
[Collection(AuthApiCollection.Name)]
public sealed class PasswordChangeApiTests : IAsyncLifetime
{
    private const string NewPassword = "Brand-New-Password-5678";
    private const string Path = "/api/users/me/password";

    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public PasswordChangeApiTests(AuthApiFixture fixture)
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
    public async Task ShouldChangePasswordAndKeepOnlyTheCurrentSessionWhenCredentialsAreValid()
    {
        var user = await _api.CreateUserAsync("change.ok");
        var current = await _api.LoginAsync("change.ok");
        var other = await _api.LoginAsync("change.ok");

        var response = await Change(current.AccessToken, TestApi.Password, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.PostAsync("/api/auth/login", new { login = "change.ok", password = TestApi.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsync("/api/auth/login", new { login = "change.ok", password = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsync("/api/auth/refresh", new { refreshToken = current.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.PostAsync("/api/auth/refresh", new { refreshToken = other.RefreshToken })).StatusCode);
        Assert.True(await _api.QueryAsync<DateTimeOffset>("SELECT password_changed_at FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }) > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task ShouldKeepAccessTokensOfOtherSessionsValidUntilTheyExpire()
    {
        await _api.CreateUserAsync("change.limit");
        var current = await _api.LoginAsync("change.limit");
        var other = await _api.LoginAsync("change.limit");

        await Change(current.AccessToken, TestApi.Password, NewPassword);
        var query = await _api.PostAsync("/api/users/search", new { login = "change.limit" }, other.AccessToken);

        // Limitacao documentada: o access token das outras sessoes vale ate expirar (no maximo 15 min).
        Assert.Equal(HttpStatusCode.OK, query.StatusCode);
    }

    [Fact]
    public async Task ShouldNotTouchOtherUsersSessionsOrPasswords()
    {
        await _api.CreateUserAsync("change.mine");
        var other = await _api.CreateUserAsync("change.theirs");
        var mine = await _api.LoginAsync("change.mine");
        var theirs = await _api.LoginAsync("change.theirs");
        var hashBefore = await HashAsync(other.ExternalId);

        await Change(mine.AccessToken, TestApi.Password, NewPassword);

        Assert.Equal(hashBefore, await HashAsync(other.ExternalId));
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsync("/api/auth/refresh", new { refreshToken = theirs.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task ShouldReturnUnauthorizedAndCountTheFailureWhenCurrentPasswordIsWrong()
    {
        var user = await _api.CreateUserAsync("change.wrong");
        var session = await _api.LoginAsync("change.wrong");
        var hashBefore = await HashAsync(user.ExternalId);

        var response = await Change(session.AccessToken, "Wrong-Password-0000", NewPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(hashBefore, await HashAsync(user.ExternalId));
        Assert.Equal(1, await _api.QueryAsync<int>("SELECT access_failed_count FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }));
        Assert.Equal(1, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
    }

    [Fact]
    public async Task ShouldLockTheAccountAfterRepeatedWrongCurrentPasswordsAndAnswerTheSame401()
    {
        var user = await _api.CreateUserAsync("change.lock");
        var session = await _api.LoginAsync("change.lock");

        HttpResponseMessage? wrong = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            wrong = await Change(session.AccessToken, "Wrong-Password-0000", NewPassword);
        }

        var locked = await Change(session.AccessToken, TestApi.Password, NewPassword);
        var login = await _api.PostAsync("/api/auth/login", new { login = "change.lock", password = TestApi.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong!.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await locked.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.NotNull(await _api.QueryAsync<DateTimeOffset?>("SELECT lockout_end FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }));
    }

    [Theory]
    [InlineData("fourteen-chars")]
    [InlineData("1q2w3e4r5t6y7u8i")]
    [InlineData("my-change.policy-passphrase")]
    public async Task ShouldReturnBadRequestAndKeepEverythingWhenNewPasswordBreaksThePolicy(string newPassword)
    {
        var user = await _api.CreateUserAsync("change.policy");
        var session = await _api.LoginAsync("change.policy");
        var hashBefore = await HashAsync(user.ExternalId);

        var response = await Change(session.AccessToken, TestApi.Password, newPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(hashBefore, await HashAsync(user.ExternalId));
        Assert.Equal(0, await _api.QueryAsync<int>("SELECT access_failed_count FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }));
    }

    [Fact]
    public async Task ShouldReturnBadRequestWhenNewPasswordHasAppearedInADataBreach()
    {
        await _api.CreateUserAsync("change.breach");
        var session = await _api.LoginAsync("change.breach");
        _fixture.Factory.BreachedPasswordChecker.MarkAsBreached("leaked change passphrase");

        var response = await Change(session.AccessToken, TestApi.Password, "leaked change passphrase");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Password is too common or has appeared in a data breach", await TestApi.ReadErrorAsync(response));
    }

    [Fact]
    public async Task ShouldReturnBadRequestWhenNewPasswordEqualsTheCurrentOne()
    {
        await _api.CreateUserAsync("change.same");
        var session = await _api.LoginAsync("change.same");

        var response = await Change(session.AccessToken, TestApi.Password, TestApi.Password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("New password must be different from the current password", await TestApi.ReadErrorAsync(response));
    }

    [Fact]
    public async Task ShouldZeroTheFailureCounterWhenThePasswordIsChanged()
    {
        var user = await _api.CreateUserAsync("change.counter");
        var session = await _api.LoginAsync("change.counter");
        await Change(session.AccessToken, "Wrong-Password-0000", NewPassword);

        var response = await Change(session.AccessToken, TestApi.Password, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await _api.QueryAsync<int>("SELECT access_failed_count FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }));
    }

    [Fact]
    public async Task ShouldReturnUnauthorizedWithoutAccessToken()
    {
        var response = await Change(null, TestApi.Password, NewPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnUnauthorizedInvalidAccessTokenWhenTheAccountWasDeleted()
    {
        var user = await _api.CreateUserAsync("change.deleted");
        var session = await _api.LoginAsync("change.deleted");
        await _api.ExecuteAsync("UPDATE auth.users SET deleted_at = SYSDATETIMEOFFSET(), active = 0 WHERE external_id = @Id;", new { Id = user.ExternalId });

        var response = await Change(session.AccessToken, TestApi.Password, NewPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Invalid access token", await TestApi.ReadErrorAsync(response));
    }

    [Fact]
    public async Task ShouldNotExposeAVariantToChangeAnotherAccountsPassword()
    {
        var other = await _api.CreateUserAsync("change.victim");
        await _api.CreateUserAsync("change.admin");
        await _api.SetRoleAsync(await _api.GetExternalIdAsync("change.admin"), Ouroboros.Auth.Domain.Entities.UserRole.Admin);
        var admin = await _api.LoginAsync("change.admin");
        var hashBefore = await HashAsync(other.ExternalId);

        var response = await _api.SendAsync(HttpMethod.Put, $"/api/users/{other.ExternalId}/password", new { currentPassword = TestApi.Password, newPassword = NewPassword }, admin.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(hashBefore, await HashAsync(other.ExternalId));
    }

    [Fact]
    public async Task ShouldKeepPasswordAndSessionsWhenRevokingTheOtherSessionsFails()
    {
        var user = await _api.CreateUserAsync("change.fault");
        var current = await _api.LoginAsync("change.fault");
        var other = await _api.LoginAsync("change.fault");
        var hashBefore = await HashAsync(user.ExternalId);

        using var factory = _fixture.CreateFactoryFailingOn<IRefreshTokenRepository>(nameof(IRefreshTokenRepository.RevokeAllActiveByUserExceptSessionAsync), FaultTiming.After);
        using var failing = new TestApi(_fixture, factory.CreateClient());

        var response = await failing.SendAsync(HttpMethod.Put, Path, new { currentPassword = TestApi.Password, newPassword = NewPassword }, current.AccessToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(hashBefore, await HashAsync(user.ExternalId));
        Assert.Equal(2, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsync("/api/auth/refresh", new { refreshToken = other.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Change(current.AccessToken, TestApi.Password, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task ShouldReturnTooManyRequestsWhenThePasswordChangeLimitIsExceeded()
    {
        await _api.CreateUserAsync("change.limit.rate");
        var session = await _api.LoginAsync("change.limit.rate");
        var ip = AuthApiFixture.NextIp();

        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt <= AuthApiFixture.LowPermitLimit; attempt++)
        {
            statuses.Add((await _api.SendAsync(HttpMethod.Put, Path, new { currentPassword = "Wrong-Password-0000", newPassword = NewPassword }, session.AccessToken, ip)).StatusCode);
        }

        Assert.All(statuses.Take(AuthApiFixture.LowPermitLimit), status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses.Last());
    }

    private Task<HttpResponseMessage> Change(string? bearer, string currentPassword, string newPassword) =>
        _api.SendAsync(HttpMethod.Put, Path, new { currentPassword, newPassword }, bearer);

    private Task<string> HashAsync(Guid externalId) =>
        _api.QueryAsync<string>("SELECT password_hash FROM auth.users WHERE external_id = @Id;", new { Id = externalId })!;
}
