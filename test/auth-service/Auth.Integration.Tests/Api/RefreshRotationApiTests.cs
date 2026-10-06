namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Infrastructure.Persistence;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092507: rotacao atomica e deteccao de reuso do refresh token, pela API contra o banco real.
[Collection(AuthApiCollection.Name)]
public sealed class RefreshRotationApiTests : IAsyncLifetime
{
    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public RefreshRotationApiTests(AuthApiFixture fixture)
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
    public async Task ShouldRevokeTheWholeSessionAndRespondWithGenericMessageWhenAnOldTokenIsReused()
    {
        var user = await _api.CreateUserAsync("reuse.user");
        var login = await _api.LoginAsync("reuse.user");
        var rotated = await TestApi.ReadTokensAsync(await _api.PostAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken }));

        var reuse = await _api.PostAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });
        var withSuccessor = await _api.PostAsync("/api/auth/refresh", new { refreshToken = rotated.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal("Invalid refresh token", await TestApi.ReadErrorAsync(reuse));
        Assert.Equal(HttpStatusCode.Unauthorized, withSuccessor.StatusCode);
        Assert.Equal(0, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
    }

    [Fact]
    public async Task ShouldNotAffectOtherSessionsOfTheSameUserWhenATokenIsReused()
    {
        var user = await _api.CreateUserAsync("reuse.other");
        var attacked = await _api.LoginAsync("reuse.other");
        var other = await _api.LoginAsync("reuse.other");
        await _api.PostAsync("/api/auth/refresh", new { refreshToken = attacked.RefreshToken });

        await _api.PostAsync("/api/auth/refresh", new { refreshToken = attacked.RefreshToken });
        var otherRefresh = await _api.PostAsync("/api/auth/refresh", new { refreshToken = other.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, otherRefresh.StatusCode);
        Assert.Equal(1, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
    }

    [Fact]
    public async Task ShouldRevokeTheSessionWhenATokenRevokedByLogoutIsReused()
    {
        var user = await _api.CreateUserAsync("reuse.logout");
        var login = await _api.LoginAsync("reuse.logout");
        await _api.PostAsync("/api/auth/logout", new { refreshToken = login.RefreshToken });

        var response = await _api.PostAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Invalid refresh token", await TestApi.ReadErrorAsync(response));
        Assert.Equal(0, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
    }

    [Fact]
    public async Task ShouldRejectAnExpiredTokenWithoutRevokingTheSession()
    {
        var user = await _api.CreateUserAsync("expired.session");
        var first = await _api.LoginAsync("expired.session");
        var rotated = await TestApi.ReadTokensAsync(await _api.PostAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken }));
        await _api.ExpireRefreshTokenAsync(rotated.RefreshToken);
        var sibling = await AddActiveTokenToTheSameSessionAsync(user.ExternalId, rotated.RefreshToken);

        var response = await _api.PostAsync("/api/auth/refresh", new { refreshToken = rotated.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Refresh token has expired", await TestApi.ReadErrorAsync(response));
        Assert.Equal(1, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsync("/api/auth/refresh", new { refreshToken = sibling })).StatusCode);
    }

    [Fact]
    public async Task ShouldKeepTheOldTokenValidWhenInsertingTheSuccessorFails()
    {
        var user = await _api.CreateUserAsync("rotation.fault");
        var login = await _api.LoginAsync("rotation.fault");

        using var factory = _fixture.CreateFactoryFailingOn<IRefreshTokenRepository>(nameof(IRefreshTokenRepository.AddAsync));
        using var failing = new TestApi(_fixture, factory.CreateClient());

        var response = await failing.PostAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });
        var retry = await _api.PostAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(1, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
    }

    [Fact]
    public async Task ShouldKeepTheOldTokenValidWhenInsertingTheSuccessorFailsAfterTheCommandRan()
    {
        var user = await _api.CreateUserAsync("rotation.fault.after");
        var login = await _api.LoginAsync("rotation.fault.after");

        using var factory = _fixture.CreateFactoryFailingOn<IRefreshTokenRepository>(nameof(IRefreshTokenRepository.AddAsync), FaultTiming.After);
        using var failing = new TestApi(_fixture, factory.CreateClient());

        var response = await failing.PostAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
        Assert.Equal(HttpStatusCode.OK, (await _api.PostAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task ShouldGiveOneSuccessAndRevokeTheSessionWhenTheSameTokenIsRefreshedAtTheSameTime()
    {
        for (var round = 0; round < 5; round++)
        {
            var login = $"race.refresh.{round}";
            var user = await _api.CreateUserAsync(login);
            var tokens = await _api.LoginAsync(login);

            // As duas requisicoes leem o token antes de qualquer uma rotaciona-lo: e a disputa que a deteccao precisa cobrir.
            var gate = new ReadGate(parties: 2);
            using var factory = _fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRefreshTokenRepository>();
                services.AddScoped<IRefreshTokenRepository>(provider =>
                    new GatedRefreshTokenRepository(ActivatorUtilities.CreateInstance<DapperRefreshTokenRepository>(provider), gate));
            }));
            using var racing = new TestApi(_fixture, factory.CreateClient());

            var responses = await Task.WhenAll(
                racing.PostAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken }),
                racing.PostAsync("/api/auth/refresh", new { refreshToken = tokens.RefreshToken }));

            var succeeded = responses.Single(response => response.StatusCode == HttpStatusCode.OK);
            var rejected = responses.Single(response => response.StatusCode == HttpStatusCode.Unauthorized);
            var winner = await TestApi.ReadTokensAsync(succeeded);

            Assert.Equal("Invalid refresh token", await TestApi.ReadErrorAsync(rejected));
            Assert.Equal(0, await _api.CountActiveRefreshTokensAsync(user.ExternalId));
            Assert.Equal(HttpStatusCode.Unauthorized, (await _api.PostAsync("/api/auth/refresh", new { refreshToken = winner.RefreshToken })).StatusCode);
        }
    }

    private async Task<string> AddActiveTokenToTheSameSessionAsync(
        Guid userExternalId,
        string sessionTokenRaw)
    {
        var generator = new Ouroboros.Auth.Infrastructure.Security.Sha256TokenGenerator();
        var sessionId = await _api.QueryAsync<Guid>(
            "SELECT session_id FROM auth.refresh_tokens WHERE token_hash = @Hash;",
            new { Hash = generator.Hash(sessionTokenRaw) });
        var raw = generator.Generate();
        var token = RefreshToken.Create(userExternalId, generator.Hash(raw), DateTimeOffset.UtcNow.AddDays(1), sessionId);

        await using var session = _fixture.CreateSession();
        await new DapperRefreshTokenRepository(session).AddAsync(token);

        return raw;
    }

    // Segura as leituras do token ate que as duas requisicoes tenham lido.
    private sealed class ReadGate
    {
        private readonly int _parties;
        private readonly TaskCompletionSource _opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public ReadGate(int parties)
        {
            _parties = parties;
        }

        public async Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _arrived) >= _parties)
            {
                _opened.TrySetResult();
            }

            await _opened.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }

    private sealed class GatedRefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly IRefreshTokenRepository _inner;
        private readonly ReadGate _gate;

        public GatedRefreshTokenRepository(
            IRefreshTokenRepository inner,
            ReadGate gate)
        {
            _inner = inner;
            _gate = gate;
        }

        public async Task<RefreshToken?> GetByHashAsync(string tokenHash)
        {
            var token = await _inner.GetByHashAsync(tokenHash);

            await _gate.ArriveAsync();

            return token;
        }

        public Task AddAsync(RefreshToken refreshToken) => _inner.AddAsync(refreshToken);

        public Task<bool> TryRevokeAsync(RefreshToken refreshToken) => _inner.TryRevokeAsync(refreshToken);

        public Task RevokeAllActiveByUserAsync(
            Guid userExternalId,
            DateTimeOffset revokedAt) => _inner.RevokeAllActiveByUserAsync(userExternalId, revokedAt);

        public Task RevokeAllActiveBySessionAsync(
            Guid sessionId,
            DateTimeOffset revokedAt) => _inner.RevokeAllActiveBySessionAsync(sessionId, revokedAt);
    }
}
