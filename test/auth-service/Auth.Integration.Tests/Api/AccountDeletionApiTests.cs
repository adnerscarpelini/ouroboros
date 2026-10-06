namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Linha de base (spec 2026092512): exclusao de conta pela API contra o banco real.
[Collection(AuthApiCollection.Name)]
public sealed class AccountDeletionApiTests : IAsyncLifetime
{
    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public AccountDeletionApiTests(AuthApiFixture fixture)
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
    public async Task ShouldReturnUnauthorizedWhenThereIsNoToken()
    {
        var user = await _api.CreateUserAsync("delete.anonymous");

        var response = await Delete(user.ExternalId, TestApi.Password, bearer: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(await DeletedAtAsync(user.ExternalId));
    }

    [Fact]
    public async Task ShouldDeleteOwnAccountAndEndSessionsAndPendingTokens()
    {
        var user = await _api.CreateUserAsync("delete.self");
        var session = await _api.LoginAsync("delete.self");
        await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);

        var response = await Delete(user.ExternalId, TestApi.Password, session.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull(await DeletedAtAsync(user.ExternalId));
        Assert.Equal(0, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
        Assert.Equal(0, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.tokens WHERE used_at IS NULL AND expires_at > SYSDATETIMEOFFSET();"));

        var login = await _api.PostAsync("/api/auth/login", new { login = "delete.self", password = TestApi.Password });
        var refresh = await _api.PostAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task ShouldKeepAccountWhenPasswordIsWrong()
    {
        var user = await _api.CreateUserAsync("delete.wrong");
        var session = await _api.LoginAsync("delete.wrong");

        var response = await Delete(user.ExternalId, "Wrong-Password-123", session.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(await DeletedAtAsync(user.ExternalId));
    }

    [Fact]
    public async Task ShouldReturnForbiddenWhenCommonUserDeletesAnotherUser()
    {
        await _api.CreateUserAsync("delete.common");
        var other = await _api.CreateUserAsync("delete.victim");
        var session = await _api.LoginAsync("delete.common");

        var response = await Delete(other.ExternalId, TestApi.Password, session.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(await DeletedAtAsync(other.ExternalId));
    }

    [Fact]
    public async Task ShouldLetAdminDeleteAnotherUserAndReturnNotFoundForUnknownOne()
    {
        var admin = await _api.CreateUserAsync("delete.admin");
        await _api.SetRoleAsync(admin.ExternalId, UserRole.Admin);
        var other = await _api.CreateUserAsync("delete.target");
        var session = await _api.LoginAsync("delete.admin");

        var deleted = await Delete(other.ExternalId, TestApi.Password, session.AccessToken);
        var unknown = await Delete(Guid.NewGuid(), TestApi.Password, session.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.NotNull(await DeletedAtAsync(other.ExternalId));
        Assert.Null(await DeletedAtAsync(admin.ExternalId));
    }

    [Fact]
    public async Task ShouldRejectDeletionOfTheLastActiveAdmin()
    {
        var admin = await _api.CreateUserAsync("delete.last.admin");
        await _api.SetRoleAsync(admin.ExternalId, UserRole.Admin);
        var session = await _api.LoginAsync("delete.last.admin");

        var response = await Delete(admin.ExternalId, TestApi.Password, session.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await DeletedAtAsync(admin.ExternalId));
    }

    [Fact]
    public async Task ShouldFreeEmailButKeepLoginReservedAfterDeletion()
    {
        var user = await _api.CreateUserAsync("delete.reuse", "reuse@example.com");
        var session = await _api.LoginAsync("delete.reuse");
        await Delete(user.ExternalId, TestApi.Password, session.AccessToken);

        var sameEmail = await _api.PostAsync(
            "/api/users",
            new { login = "delete.reuse.new", fullName = "Reuse", email = "reuse@example.com", password = TestApi.Password });
        var sameLogin = await _api.PostAsync(
            "/api/users",
            new { login = "delete.reuse", fullName = "Reuse", email = "reuse.other@example.com", password = TestApi.Password });

        Assert.Equal(HttpStatusCode.Accepted, sameEmail.StatusCode);
        Assert.Equal(1, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.users WHERE login = 'delete.reuse.new';"));
        Assert.Equal(HttpStatusCode.BadRequest, sameLogin.StatusCode);
    }

    private Task<HttpResponseMessage> Delete(Guid externalId, string password, string? bearer) =>
        _api.SendAsync(HttpMethod.Delete, $"/api/users/{externalId}", new { password }, bearer);

    private Task<DateTimeOffset?> DeletedAtAsync(Guid externalId) =>
        _api.QueryAsync<DateTimeOffset?>("SELECT deleted_at FROM auth.users WHERE external_id = @Id;", new { Id = externalId });
}
