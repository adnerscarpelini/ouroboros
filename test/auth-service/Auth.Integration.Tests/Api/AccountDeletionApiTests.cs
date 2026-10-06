namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using Ouroboros.Auth.Application.Gateways;
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

    // Spec 2026092505

    [Theory]
    [InlineData(FaultTiming.Before)]
    [InlineData(FaultTiming.After)]
    public async Task ShouldKeepAccountSessionsAndTokensWhenSessionRevocationFails(FaultTiming timing)
    {
        var user = await _api.CreateUserAsync($"delete.rollback.{timing}");
        var session = await _api.LoginAsync($"delete.rollback.{timing}");
        await _api.AddTokenAsync(user.ExternalId, TokenType.PasswordReset);

        using var factory = _fixture.CreateFactoryFailingOn<IRefreshTokenRepository>(
            nameof(IRefreshTokenRepository.RevokeAllActiveByUserAsync),
            timing);
        using var failing = new TestApi(_fixture, factory.CreateClient());

        var response = await failing.SendAsync(
            HttpMethod.Delete,
            $"/api/users/{user.ExternalId}",
            new { password = TestApi.Password },
            session.AccessToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(await DeletedAtAsync(user.ExternalId));
        Assert.True(await _api.QueryAsync<bool>("SELECT active FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }));
        Assert.Equal(1, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
        Assert.Equal(1, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.tokens WHERE used_at IS NULL AND expires_at > SYSDATETIMEOFFSET();"));
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken })).StatusCode);

        // A conta continua inteira e a exclusao funciona numa nova tentativa, sem a falha.
        var retry = await Delete(user.ExternalId, TestApi.Password, session.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
    }

    [Fact]
    public async Task ShouldCountWrongPasswordTowardLockoutEvenThoughDeletionFails()
    {
        var user = await _api.CreateUserAsync("delete.count.failure");
        var session = await _api.LoginAsync("delete.count.failure");

        var response = await Delete(user.ExternalId, "Wrong-Password-123", session.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, await _api.QueryAsync<int>("SELECT access_failed_count FROM auth.users WHERE external_id = @Id;", new { Id = user.ExternalId }));
    }

    [Fact]
    public async Task ShouldLetOnlyOneOfTwoAdminsDeleteTheOtherWhenTheyActAtTheSameTime()
    {
        for (var round = 0; round < 8; round++)
        {
            await _fixture.ResetDatabaseAsync();

            var first = await _api.CreateUserAsync("race.admin.one");
            var second = await _api.CreateUserAsync("race.admin.two");
            await _api.SetRoleAsync(first.ExternalId, UserRole.Admin);
            await _api.SetRoleAsync(second.ExternalId, UserRole.Admin);
            var firstSession = await _api.LoginAsync("race.admin.one");
            var secondSession = await _api.LoginAsync("race.admin.two");

            var responses = await Task.WhenAll(
                Delete(second.ExternalId, TestApi.Password, firstSession.AccessToken),
                Delete(first.ExternalId, TestApi.Password, secondSession.AccessToken));

            // A propriedade que importa: nunca dois 204 (que deixariam o sistema sem Admin) e nunca 500. Quem chega junto
            // espera o lock e recebe 400. Quem chega depois do commit do outro ja foi excluido, e o token de conta
            // excluida da 401. Pela regra de hints (leituras de decisao com READPAST), uma linha que a outra requisicao
            // esta alterando naquele instante some para quem le: o alvo "nao existe" e a resposta e 404. Os tres desfechos
            // dependem so do tempo de cada requisicao.
            var succeeded = responses.Single(response => response.StatusCode == HttpStatusCode.NoContent);
            var rejected = responses.Single(response => response != succeeded);

            Assert.Contains(rejected.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound });
            Assert.Equal(1, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.users WHERE role = 'Admin' AND active = 1 AND deleted_at IS NULL;"));

            if (rejected.StatusCode == HttpStatusCode.BadRequest)
            {
                Assert.Equal("The last active admin cannot be deleted", await TestApi.ReadErrorAsync(rejected));
            }
        }
    }

    // Spec 2026092510: o privilegio vem do banco, nao do token.

    [Fact]
    public async Task ShouldDenyDemotedAdminImmediatelyEvenWithTheOldTokenStillValid()
    {
        var admin = await _api.CreateUserAsync("delete.demoted.admin");
        await _api.SetRoleAsync(admin.ExternalId, UserRole.Admin);
        var victim = await _api.CreateUserAsync("delete.demoted.victim");
        var session = await _api.LoginAsync("delete.demoted.admin");

        await _api.SetRoleAsync(admin.ExternalId, UserRole.User);

        var response = await Delete(victim.ExternalId, TestApi.Password, session.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(await DeletedAtAsync(victim.ExternalId));
    }

    [Fact]
    public async Task ShouldReturnUnauthorizedWhenRequesterWasDeletedAndTokenIsStillValid()
    {
        var user = await _api.CreateUserAsync("delete.gone.requester");
        var other = await _api.CreateUserAsync("delete.gone.target");
        var session = await _api.LoginAsync("delete.gone.requester");
        await _api.ExecuteAsync("UPDATE auth.users SET deleted_at = SYSDATETIMEOFFSET(), active = 0 WHERE external_id = @Id;", new { Id = user.ExternalId });

        var self = await Delete(user.ExternalId, TestApi.Password, session.AccessToken);
        var another = await Delete(other.ExternalId, TestApi.Password, session.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, self.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, another.StatusCode);
        Assert.Equal("Invalid access token", await TestApi.ReadErrorAsync(another));
        Assert.Null(await DeletedAtAsync(other.ExternalId));
    }

    private Task<HttpResponseMessage> Delete(Guid externalId, string password, string? bearer) =>
        _api.SendAsync(HttpMethod.Delete, $"/api/users/{externalId}", new { password }, bearer);

    private Task<DateTimeOffset?> DeletedAtAsync(Guid externalId) =>
        _api.QueryAsync<DateTimeOffset?>("SELECT deleted_at FROM auth.users WHERE external_id = @Id;", new { Id = externalId });
}
