namespace Ouroboros.Auth.Application.UseCases.RefreshAccessToken;

using Ouroboros.Auth.Application.Fakes;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.Settings;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class RefreshAccessTokenInteractorTests
{
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

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Items { get; } = new();

        public List<RefreshToken> Added { get; } = new();

        public List<RefreshToken> Revoked { get; } = new();

        // Simula outra requisicao que revogou o mesmo token no banco antes desta.
        public bool RevokedConcurrently { get; set; }

        public Exception? AddException { get; set; }

        public Task AddAsync(RefreshToken refreshToken)
        {
            if (AddException is not null)
            {
                throw AddException;
            }

            Items.Add(refreshToken);
            Added.Add(refreshToken);
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> GetByHashAsync(string tokenHash)
        {
            var refreshToken = Items.FirstOrDefault(item => item.TokenHash == tokenHash);
            return Task.FromResult(refreshToken);
        }

        public Task<bool> TryRevokeAsync(RefreshToken refreshToken)
        {
            if (RevokedConcurrently)
            {
                return Task.FromResult(false);
            }

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

    private sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
    {
        public static readonly DateTimeOffset ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        public Guid? LastSessionId { get; private set; }

        public AccessToken Generate(
            Guid userId,
            string login,
            string email,
            UserRole role,
            Guid sessionId)
        {
            LastSessionId = sessionId;
            return new AccessToken($"jwt:{userId}:{login}:{email}:{role}", ExpiresAt);
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

    private static readonly RefreshTokenSettings RefreshTokenSettings = new(TimeSpan.FromDays(7));

    private static User CreateUser(bool active)
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed-password");

        if (active)
        {
            user.ConfirmEmail();
        }

        return user;
    }

    private static User CreateActiveUserWithRole(UserRole role)
    {
        return User.Rehydrate(
            1,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(-30),
            null,
            "jdoe",
            "John Doe",
            "jdoe@example.com",
            true,
            "hashed-password",
            DateTimeOffset.UtcNow.AddDays(-30),
            true,
            null,
            role,
            null);
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

    private static RefreshAccessTokenInteractor CreateInteractor(
        FakeUserRepository userRepository,
        FakeRefreshTokenRepository refreshTokenRepository,
        FakeJwtTokenGenerator? jwtTokenGenerator = null,
        FakeUnitOfWork? unitOfWork = null)
    {
        return new RefreshAccessTokenInteractor(
            userRepository,
            refreshTokenRepository,
            jwtTokenGenerator ?? new FakeJwtTokenGenerator(),
            new FakeTokenGenerator(),
            RefreshTokenSettings,
            unitOfWork ?? new FakeUnitOfWork());
    }

    [Fact]
    public async Task ShouldIssueNewPairAndRevokeCurrentTokenWhenRefreshTokenIsValid()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        var currentToken = CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), null);
        refreshTokenRepository.Items.Add(currentToken);
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        var response = await interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token"));

        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal($"jwt:{user.ExternalId}:jdoe:jdoe@example.com:User", response.AccessToken);
        Assert.Equal(FakeJwtTokenGenerator.ExpiresAt, response.AccessTokenExpiresAt);
        Assert.Equal("new-refresh-token", response.RefreshToken);
        Assert.Single(refreshTokenRepository.Revoked);
        Assert.Same(currentToken, refreshTokenRepository.Revoked[0]);
        Assert.NotNull(currentToken.RevokedAt);
        Assert.Single(refreshTokenRepository.Added);
        Assert.Equal(user.ExternalId, refreshTokenRepository.Added[0].UserExternalId);
        Assert.Equal("hashed:new-refresh-token", refreshTokenRepository.Added[0].TokenHash);
        Assert.Equal(response.RefreshTokenExpiresAt, refreshTokenRepository.Added[0].ExpiresAt);
        Assert.Null(refreshTokenRepository.Added[0].RevokedAt);
    }

    [Fact]
    public async Task ShouldInheritSessionIdInTheSuccessorTokenWhenRefreshTokenIsRotated()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), null));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token"));

        Assert.Equal(SessionId, Assert.Single(refreshTokenRepository.Added).SessionId);
    }

    [Fact]
    public async Task ShouldKeepSessionIdClaimInTheNewAccessTokenWhenRefreshTokenIsRotated()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var jwtTokenGenerator = new FakeJwtTokenGenerator();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), null));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository, jwtTokenGenerator);

        await interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token"));

        Assert.Equal(SessionId, jwtTokenGenerator.LastSessionId);
    }

    [Fact]
    public async Task ShouldKeepUserRoleWhenAccessTokenIsRefreshed()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateActiveUserWithRole(UserRole.Admin);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), null));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        var response = await interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token"));

        Assert.Equal($"jwt:{user.ExternalId}:jdoe:jdoe@example.com:Admin", response.AccessToken);
    }

    [Fact]
    public async Task ShouldRejectWithoutRevokingTheSessionWhenTokenIsExpired()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddMinutes(-1), null));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        var exception = await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token")));

        Assert.IsNotType<RefreshTokenReuseException>(exception);
        Assert.Equal("Refresh token has expired", exception.Message);
        Assert.Empty(refreshTokenRepository.SessionRevocations);
        Assert.Empty(refreshTokenRepository.Revoked);
        Assert.Empty(refreshTokenRepository.Added);
    }

    [Fact]
    public async Task ShouldRevokeTheSessionAndRejectWithGenericMessageWhenTokenWasAlreadyRevoked()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddHours(-1)));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        var exception = await Assert.ThrowsAsync<RefreshTokenReuseException>(() => interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token")));

        Assert.Equal("Invalid refresh token", exception.Message);
        Assert.Equal(user.ExternalId, exception.UserExternalId);
        Assert.Equal(SessionId, exception.SessionId);
        Assert.Equal(SessionId, Assert.Single(refreshTokenRepository.SessionRevocations));
        Assert.Empty(refreshTokenRepository.Revoked);
        Assert.Empty(refreshTokenRepository.Added);
    }

    [Fact]
    public async Task ShouldRevokeOnlyTheSessionOfTheReusedTokenWhenTokenWasAlreadyRevoked()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddHours(-1)));
        var otherSessionToken = RefreshToken.Create(user.ExternalId, "hashed:other-session", DateTimeOffset.UtcNow.AddDays(1));
        refreshTokenRepository.Items.Add(otherSessionToken);
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await Assert.ThrowsAsync<RefreshTokenReuseException>(() => interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token")));

        Assert.DoesNotContain(otherSessionToken.SessionId, refreshTokenRepository.SessionRevocations);
        Assert.Null(otherSessionToken.RevokedAt);
    }

    [Fact]
    public async Task ShouldRevokeTheSessionWhenTokenIsReusedAfterRotation()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), null));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token"));

        await Assert.ThrowsAsync<RefreshTokenReuseException>(() => interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token")));
        Assert.Single(refreshTokenRepository.Revoked);
        Assert.Single(refreshTokenRepository.Added);
        Assert.Equal(SessionId, Assert.Single(refreshTokenRepository.SessionRevocations));
    }

    [Fact]
    public async Task ShouldTreatLosingTheRaceForTheTokenAsReuseAndRevokeTheSession()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository { RevokedConcurrently = true };
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), null));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await Assert.ThrowsAsync<RefreshTokenReuseException>(() => interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token")));

        Assert.Empty(refreshTokenRepository.Added);
        Assert.Equal(SessionId, Assert.Single(refreshTokenRepository.SessionRevocations));
    }

    [Fact]
    public async Task ShouldRunRotationInsideTheSameUnitOfWorkAndCommitOnce()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), null));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository, unitOfWork: unitOfWork);

        await interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token"));

        Assert.Equal(1, unitOfWork.Commits);
        Assert.Equal(0, unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldRollBackAndNotRevokeTheSessionWhenInsertingTheSuccessorFails()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository { AddException = new InvalidOperationException("insert failed") };
        var unitOfWork = new FakeUnitOfWork();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), null));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository, unitOfWork: unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() => interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token")));

        Assert.Equal(0, unitOfWork.Commits);
        Assert.Equal(1, unitOfWork.Rollbacks);
        Assert.Empty(refreshTokenRepository.SessionRevocations);
    }

    [Fact]
    public async Task ShouldThrowInvalidRefreshTokenExceptionWhenTokenDoesNotExist()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), null));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => interactor.ExecuteAsync(new RefreshAccessTokenRequest("unknown-refresh-token")));
        Assert.Empty(refreshTokenRepository.Revoked);
        Assert.Empty(refreshTokenRepository.Added);
    }

    [Fact]
    public async Task ShouldThrowInvalidRefreshTokenExceptionWhenTokenIsBlank()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => interactor.ExecuteAsync(new RefreshAccessTokenRequest("   ")));
        Assert.Empty(refreshTokenRepository.Added);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenUserIsInactive()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: false);
        userRepository.Items.Add(user);
        var currentToken = CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), null);
        refreshTokenRepository.Items.Add(currentToken);
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        var exception = await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token")));

        Assert.IsNotType<InvalidRefreshTokenException>(exception);
        Assert.Null(currentToken.RevokedAt);
        Assert.Empty(refreshTokenRepository.Added);
    }
}
