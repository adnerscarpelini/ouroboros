namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Linha de base (spec 2026092512): confirmacao de e-mail pela API contra o banco real.
[Collection(AuthApiCollection.Name)]
public sealed class EmailConfirmationApiTests : IAsyncLifetime
{
    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public EmailConfirmationApiTests(AuthApiFixture fixture)
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
    public async Task ShouldActivateUserAndConsumeTokenWhenTokenIsValid()
    {
        var user = await _api.CreateUserAsync("pending.user", confirmed: false);
        var token = await _api.AddTokenAsync(user.ExternalId, TokenType.EmailConfirmation);

        var response = await Confirm(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await IsActiveAsync(user.ExternalId));
        Assert.Equal(1, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.tokens WHERE used_at IS NOT NULL;"));
    }

    [Fact]
    public async Task ShouldAllowLoginOnlyAfterConfirmation()
    {
        var user = await _api.CreateUserAsync("confirmed.login", confirmed: false);
        var token = await _api.AddTokenAsync(user.ExternalId, TokenType.EmailConfirmation);

        var beforeConfirmation = await _api.PostAsync("/api/auth/login", new { login = "confirmed.login", password = TestApi.Password });
        await Confirm(token);
        var afterConfirmation = await _api.PostAsync("/api/auth/login", new { login = "confirmed.login", password = TestApi.Password });

        Assert.Equal(HttpStatusCode.BadRequest, beforeConfirmation.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterConfirmation.StatusCode);
    }

    [Fact]
    public async Task ShouldRejectSecondConfirmationWithTheSameToken()
    {
        var user = await _api.CreateUserAsync("twice.user", confirmed: false);
        var token = await _api.AddTokenAsync(user.ExternalId, TokenType.EmailConfirmation);

        await Confirm(token);
        var second = await Confirm(token);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task ShouldRejectUnknownToken()
    {
        var response = await Confirm("unknown-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ShouldRejectExpiredTokenAndKeepUserInactive()
    {
        var user = await _api.CreateUserAsync("expired.user", confirmed: false);
        var token = await _api.AddTokenAsync(user.ExternalId, TokenType.EmailConfirmation);
        await _api.ExpireTokenAsync(token);

        var response = await Confirm(token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await IsActiveAsync(user.ExternalId));
    }

    [Fact]
    public async Task ShouldRejectPasswordResetTokenAsConfirmationToken()
    {
        var user = await _api.CreateUserAsync("wrong.type", confirmed: false);
        var token = await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);

        var response = await Confirm(token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await IsActiveAsync(user.ExternalId));
    }

    private Task<HttpResponseMessage> Confirm(string token) =>
        _api.PostAsync("/api/users/confirm-email", new { token });

    private Task<bool> IsActiveAsync(Guid externalId) =>
        _api.QueryAsync<bool>("SELECT active FROM auth.users WHERE external_id = @Id;", new { Id = externalId });
}
