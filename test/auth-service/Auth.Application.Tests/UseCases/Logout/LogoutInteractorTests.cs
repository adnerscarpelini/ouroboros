namespace Ouroboros.Auth.Application.UseCases.Logout;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.Settings;
using Ouroboros.Auth.Application.UseCases.RefreshAccessToken;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class LogoutInteractorTests
{
    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Items { get; } = new();

        public List<RefreshToken> Revoked { get; } = new();

        public Task AddAsync(RefreshToken refreshToken)
        {
            Items.Add(refreshToken);
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> GetByHashAsync(string tokenHash)
        {
            var refreshToken = Items.FirstOrDefault(item => item.TokenHash == tokenHash);
            return Task.FromResult(refreshToken);
        }

        public Task<bool> TryRevokeAsync(RefreshToken refreshToken)
        {
            Revoked.Add(refreshToken);
            return Task.FromResult(true);
        }

        public Task RevokeAllActiveByUserAsync(Guid userExternalId, DateTimeOffset revokedAt)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Items { get; } = new();

        public Task AddAsync(User user)
        {
            Items.Add(user);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid externalId)
        {
            return Task.CompletedTask;
        }

        public Task<User?> GetByExternalIdAsync(Guid externalId)
        {
            var user = Items.FirstOrDefault(item => item.ExternalId == externalId);
            return Task.FromResult(user);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            var user = Items.FirstOrDefault(item => item.Email == email);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginAsync(string login)
        {
            var user = Items.FirstOrDefault(item => item.Login == login);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginOrEmailAsync(string loginOrEmail)
        {
            var user = Items.FirstOrDefault(item => item.Login == loginOrEmail || item.Email == loginOrEmail);
            return Task.FromResult(user);
        }

        public Task UpdateAsync(User user)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
    {
        public AccessToken Generate(
            Guid userId,
            string login,
            string email,
            UserRole role)
        {
            return new AccessToken("jwt", DateTimeOffset.UtcNow.AddMinutes(15));
        }
    }

    private sealed class FakeTokenGenerator : ITokenGenerator
    {
        public string Generate()
        {
            return "new-refresh-token";
        }

        public string Hash(string token)
        {
            return $"hashed:{token}";
        }
    }

    private static RefreshToken CreateStoredToken(
        Guid userExternalId,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt)
    {
        return RefreshToken.Rehydrate(
            1,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(-8),
            null,
            userExternalId,
            "hashed:current-refresh-token",
            expiresAt,
            revokedAt);
    }

    [Fact]
    public async Task ShouldRevokeCurrentTokenWhenTokenIsActive()
    {
        var repository = new FakeRefreshTokenRepository();
        var currentToken = CreateStoredToken(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), null);
        repository.Items.Add(currentToken);
        var interactor = new LogoutInteractor(repository, new FakeTokenGenerator());

        var response = await interactor.ExecuteAsync(new LogoutRequest("current-refresh-token"));

        Assert.True(response.Revoked);
        Assert.Single(repository.Revoked);
        Assert.Same(currentToken, repository.Revoked[0]);
        Assert.NotNull(currentToken.RevokedAt);
    }

    [Fact]
    public async Task ShouldDoNothingWhenTokenWasAlreadyRevoked()
    {
        var repository = new FakeRefreshTokenRepository();
        var revokedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var currentToken = CreateStoredToken(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), revokedAt);
        repository.Items.Add(currentToken);
        var interactor = new LogoutInteractor(repository, new FakeTokenGenerator());

        var response = await interactor.ExecuteAsync(new LogoutRequest("current-refresh-token"));

        Assert.False(response.Revoked);
        Assert.Empty(repository.Revoked);
        Assert.Equal(revokedAt, currentToken.RevokedAt);
    }

    [Fact]
    public async Task ShouldDoNothingWhenTokenIsExpired()
    {
        var repository = new FakeRefreshTokenRepository();
        var currentToken = CreateStoredToken(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), null);
        repository.Items.Add(currentToken);
        var interactor = new LogoutInteractor(repository, new FakeTokenGenerator());

        var response = await interactor.ExecuteAsync(new LogoutRequest("current-refresh-token"));

        Assert.False(response.Revoked);
        Assert.Empty(repository.Revoked);
        Assert.Null(currentToken.RevokedAt);
    }

    [Fact]
    public async Task ShouldThrowInvalidRefreshTokenExceptionWhenTokenDoesNotExist()
    {
        var repository = new FakeRefreshTokenRepository();
        repository.Items.Add(CreateStoredToken(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), null));
        var interactor = new LogoutInteractor(repository, new FakeTokenGenerator());

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => interactor.ExecuteAsync(new LogoutRequest("unknown-refresh-token")));
        Assert.Empty(repository.Revoked);
    }

    [Fact]
    public async Task ShouldThrowInvalidRefreshTokenExceptionWhenTokenIsBlank()
    {
        var repository = new FakeRefreshTokenRepository();
        var interactor = new LogoutInteractor(repository, new FakeTokenGenerator());

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => interactor.ExecuteAsync(new LogoutRequest("   ")));
        Assert.Empty(repository.Revoked);
    }

    [Fact]
    public async Task ShouldRejectRefreshWhenTokenWasRevokedByLogout()
    {
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var userRepository = new FakeUserRepository();
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed-password");
        user.ConfirmEmail();
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), null));
        var logoutInteractor = new LogoutInteractor(refreshTokenRepository, new FakeTokenGenerator());
        var refreshInteractor = new RefreshAccessTokenInteractor(
            userRepository,
            refreshTokenRepository,
            new FakeJwtTokenGenerator(),
            new FakeTokenGenerator(),
            new RefreshTokenSettings(TimeSpan.FromDays(7)));

        await logoutInteractor.ExecuteAsync(new LogoutRequest("current-refresh-token"));

        var exception = await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => refreshInteractor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token")));
        Assert.Equal("Refresh token has been revoked", exception.Message);
        Assert.Single(refreshTokenRepository.Items);
    }
}
