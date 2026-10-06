namespace Ouroboros.Auth.Application.UseCases.Logout;

using Ouroboros.Auth.Application.Fakes;
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

        public Task RevokeAllActiveByUserExceptSessionAsync(
            Guid userExternalId,
            Guid exceptSessionId,
            DateTimeOffset revokedAt)
        {
            foreach (var token in Items.Where(item => item.UserExternalId == userExternalId && item.SessionId != exceptSessionId && item.IsActive(revokedAt)))
            {
                token.Revoke(revokedAt);
            }

            return Task.CompletedTask;
        }

        public Task<int> DeleteExpiredBatchAsync(DateTimeOffset expiredBefore, int batchSize)
        {
            throw new NotSupportedException();
        }

        public Task RevokeAllActiveBySessionAsync(Guid sessionId, DateTimeOffset revokedAt)
        {
            SessionRevocations.Add(sessionId);
            return Task.CompletedTask;
        }

        public List<Guid> SessionRevocations { get; } = new();

        public Task RevokeAllActiveByUserAsync(Guid userExternalId, DateTimeOffset revokedAt)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<bool> RecordFailedAccessAsync(
            Guid externalId,
            DateTimeOffset now)
        {
            throw new NotSupportedException();
        }
        public Task<bool> TryRehashPasswordAsync(
            Guid externalId,
            string currentPasswordHash,
            string newPasswordHash)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryRegisterLoginAsync(
            Guid externalId,
            DateTimeOffset now)
        {
            throw new NotSupportedException();
        }

        public Task ClearLockoutAsync(
            Guid externalId,
            DateTimeOffset now)
        {
            throw new NotSupportedException();
        }
        public Task<bool> TryResetFailedAccessAsync(
            Guid externalId,
            DateTimeOffset now)
        {
            throw new NotSupportedException();
        }
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

        public Task<bool> ExistsDeletedByLoginAsync(string login)
        {
            return Task.FromResult(false);
        }

        public Task<int> CountActiveAdminsForUpdateAsync()
        {
            return Task.FromResult(0);
        }

        public Task<User?> GetByExternalIdAsync(Guid externalId)
        {
            var user = Items.FirstOrDefault(item => item.ExternalId == externalId);
            return Task.FromResult(user);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            var user = Items.FirstOrDefault(item => item.NormalizedEmail == email);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginAsync(string login)
        {
            var user = Items.FirstOrDefault(item => item.NormalizedLogin == login);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginOrEmailAsync(string loginOrEmail)
        {
            var user = Items.FirstOrDefault(item => item.NormalizedLogin == loginOrEmail || item.NormalizedEmail == loginOrEmail);
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
            UserRole role,
            Guid sessionId)
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

    private static readonly Guid SessionId = Guid.NewGuid();

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
            SessionId,
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
            new RefreshTokenSettings(TimeSpan.FromDays(7)),
            new FakeUnitOfWork());

        await logoutInteractor.ExecuteAsync(new LogoutRequest("current-refresh-token"));

        // Token revogado por logout e apresentado de novo conta como reuso (spec 2026092507): inofensivo, a sessao ja acabou.
        var exception = await Assert.ThrowsAsync<RefreshTokenReuseException>(() => refreshInteractor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token")));
        Assert.Equal("Invalid refresh token", exception.Message);
        Assert.Equal(SessionId, Assert.Single(refreshTokenRepository.SessionRevocations));
        Assert.Single(refreshTokenRepository.Items);
    }
}
