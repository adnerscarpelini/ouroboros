namespace Ouroboros.Auth.Application.UseCases.RefreshAccessToken;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.Settings;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class RefreshAccessTokenInteractorTests
{
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

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Items { get; } = new();

        public List<RefreshToken> Added { get; } = new();

        public List<RefreshToken> Revoked { get; } = new();

        // Simula outra requisicao que revogou o mesmo token no banco antes desta.
        public bool RevokedConcurrently { get; set; }

        public Task AddAsync(RefreshToken refreshToken)
        {
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

        public Task RevokeAllActiveByUserAsync(Guid userExternalId, DateTimeOffset revokedAt)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
    {
        public static readonly DateTimeOffset ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        public AccessToken Generate(
            Guid userId,
            string login,
            string email,
            UserRole role)
        {
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
            role);
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

    private static RefreshAccessTokenInteractor CreateInteractor(
        FakeUserRepository userRepository,
        FakeRefreshTokenRepository refreshTokenRepository)
    {
        return new RefreshAccessTokenInteractor(
            userRepository,
            refreshTokenRepository,
            new FakeJwtTokenGenerator(),
            new FakeTokenGenerator(),
            RefreshTokenSettings);
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
    public async Task ShouldThrowInvalidRefreshTokenExceptionWhenTokenIsExpired()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddMinutes(-1), null));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        var exception = await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token")));

        Assert.Equal("Refresh token has expired", exception.Message);
        Assert.Empty(refreshTokenRepository.Revoked);
        Assert.Empty(refreshTokenRepository.Added);
    }

    [Fact]
    public async Task ShouldThrowInvalidRefreshTokenExceptionWhenTokenWasAlreadyRevoked()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddHours(-1)));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        var exception = await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token")));

        Assert.Equal("Refresh token has been revoked", exception.Message);
        Assert.Empty(refreshTokenRepository.Revoked);
        Assert.Empty(refreshTokenRepository.Added);
    }

    [Fact]
    public async Task ShouldThrowInvalidRefreshTokenExceptionWhenTokenIsReusedAfterRotation()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), null));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token"));

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token")));
        Assert.Single(refreshTokenRepository.Revoked);
        Assert.Single(refreshTokenRepository.Added);
    }

    [Fact]
    public async Task ShouldThrowInvalidRefreshTokenExceptionWhenTokenIsRevokedConcurrently()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository { RevokedConcurrently = true };
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        refreshTokenRepository.Items.Add(CreateStoredToken(user.ExternalId, DateTimeOffset.UtcNow.AddDays(1), null));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => interactor.ExecuteAsync(new RefreshAccessTokenRequest("current-refresh-token")));
        Assert.Empty(refreshTokenRepository.Added);
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
