namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Linha de base (spec 2026092512): recuperacao e redefinicao de senha pela API contra o banco real.
[Collection(AuthApiCollection.Name)]
public sealed class PasswordResetApiTests : IAsyncLifetime
{
    private const string NewPassword = "Another-Password-456";

    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public PasswordResetApiTests(AuthApiFixture fixture)
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
    public async Task ShouldCreateResetTokenWhenAccountExists()
    {
        var user = await _api.CreateUserAsync("reset.request");

        var response = await RequestReset("reset.request");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, await _api.CountTokensAsync(user.ExternalId, TokenType.PasswordReset));
    }

    [Fact]
    public async Task ShouldReturnSameBodyAndCreateNothingWhenAccountDoesNotExist()
    {
        await _api.CreateUserAsync("reset.exists");

        var existing = await RequestReset("reset.exists");
        var unknown = await RequestReset("nobody@example.com");

        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await existing.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Equal(1, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.tokens;"));
    }

    [Fact]
    public async Task ShouldChangePasswordConsumeTokenAndEndSessionsWhenTokenIsValid()
    {
        var user = await _api.CreateUserAsync("reset.confirm");
        var session = await _api.LoginAsync("reset.confirm");
        var token = await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);

        var response = await Confirm(token, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await _api.CountActiveRefreshTokensAsync(user.ExternalId));

        var refresh = await _api.PostAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });
        var oldPassword = await _api.PostAsync("/api/auth/login", new { login = "reset.confirm", password = TestApi.Password });
        var newPassword = await _api.PostAsync("/api/auth/login", new { login = "reset.confirm", password = NewPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
    }

    [Fact]
    public async Task ShouldRejectSecondUseOfTheSameToken()
    {
        var user = await _api.CreateUserAsync("reset.twice");
        var token = await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);

        await Confirm(token, NewPassword);
        var second = await Confirm(token, "Yet-Another-Password-789");

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsync("/api/auth/login", new { login = "reset.twice", password = NewPassword })).StatusCode);
    }

    [Fact]
    public async Task ShouldRejectExpiredToken()
    {
        var user = await _api.CreateUserAsync("reset.expired");
        var token = await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);
        await _api.ExpireTokenAsync(token);

        var response = await Confirm(token, NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsync("/api/auth/login", new { login = "reset.expired", password = TestApi.Password })).StatusCode);
    }

    [Fact]
    public async Task ShouldRejectEmailConfirmationTokenAsResetToken()
    {
        var user = await _api.CreateUserAsync("reset.wrong.type");
        var token = await _api.AddTokenAsync(user.ExternalId, TokenType.EmailConfirmation);

        var response = await Confirm(token, NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ShouldKeepTokenUsableWhenNewPasswordIsRejected()
    {
        var user = await _api.CreateUserAsync("reset.weak");
        var token = await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);

        var weak = await Confirm(token, "short");
        var strong = await Confirm(token, NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, strong.StatusCode);
    }

    [Fact]
    public async Task ShouldRejectNewPasswordEqualToTheCurrentOne()
    {
        var user = await _api.CreateUserAsync("reset.same");
        var token = await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);

        var response = await Confirm(token, TestApi.Password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<HttpResponseMessage> RequestReset(string loginOrEmail) =>
        _api.PostAsync("/api/users/password-reset/request", new { loginOrEmail });

    private Task<HttpResponseMessage> Confirm(string token, string newPassword) =>
        _api.PostAsync("/api/users/password-reset/confirm", new { token, newPassword });
}
