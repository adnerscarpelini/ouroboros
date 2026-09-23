namespace Ouroboros.Auth.Application.UseCases.Login;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.Settings;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class LoginInteractorTests
{
    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Items { get; } = new();

        public Task AddAsync(User user)
        {
            Items.Add(user);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsByLoginOrEmailAsync(string login, string email)
        {
            var exists = Items.Any(item => item.Login == login || item.Email == email);
            return Task.FromResult(exists);
        }

        public Task<User?> GetByExternalIdAsync(Guid externalId)
        {
            var user = Items.FirstOrDefault(item => item.ExternalId == externalId);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginAsync(string login)
        {
            var user = Items.FirstOrDefault(item => item.Login == login);
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
            return Task.FromResult(true);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password)
        {
            return $"hashed:{password}";
        }

        public bool Verify(string password, string passwordHash)
        {
            return passwordHash == Hash(password);
        }
    }

    private sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
    {
        public static readonly DateTimeOffset ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        public AccessToken Generate(
            Guid userId,
            string login,
            string email)
        {
            return new AccessToken($"jwt:{userId}:{login}:{email}", ExpiresAt);
        }
    }

    private sealed class FakeTokenGenerator : ITokenGenerator
    {
        public string Generate()
        {
            return "raw-refresh-token";
        }

        public string Hash(string token)
        {
            return $"hashed:{token}";
        }
    }

    private static readonly RefreshTokenSettings RefreshTokenSettings = new(TimeSpan.FromDays(7));

    private static User CreateUser(bool active)
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed:S3cret!1");

        if (active)
        {
            user.ConfirmEmail();
        }

        return user;
    }

    private static LoginInteractor CreateInteractor(
        FakeUserRepository userRepository,
        FakeRefreshTokenRepository refreshTokenRepository)
    {
        return new LoginInteractor(
            userRepository,
            refreshTokenRepository,
            new FakePasswordHasher(),
            new FakeJwtTokenGenerator(),
            new FakeTokenGenerator(),
            RefreshTokenSettings);
    }

    [Fact]
    public async Task ShouldIssueAccessAndRefreshTokensWhenCredentialsAreValid()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        var response = await interactor.ExecuteAsync(new LoginRequest("jdoe", "S3cret!1"));

        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal($"jwt:{user.ExternalId}:jdoe:jdoe@example.com", response.AccessToken);
        Assert.Equal(FakeJwtTokenGenerator.ExpiresAt, response.AccessTokenExpiresAt);
        Assert.Equal("raw-refresh-token", response.RefreshToken);
        Assert.Single(refreshTokenRepository.Items);
        Assert.Equal(user.ExternalId, refreshTokenRepository.Items[0].UserExternalId);
        Assert.Equal("hashed:raw-refresh-token", refreshTokenRepository.Items[0].TokenHash);
        Assert.Equal(response.RefreshTokenExpiresAt, refreshTokenRepository.Items[0].ExpiresAt);
        Assert.True(response.RefreshTokenExpiresAt > DateTimeOffset.UtcNow.AddDays(6));
        Assert.Null(refreshTokenRepository.Items[0].RevokedAt);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenUserIsInactive()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        userRepository.Items.Add(CreateUser(active: false));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        var exception = await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "S3cret!1")));

        Assert.IsNotType<InvalidCredentialsException>(exception);
        Assert.Empty(refreshTokenRepository.Items);
    }

    [Fact]
    public async Task ShouldThrowInvalidCredentialsExceptionWhenPasswordIsIncorrect()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        userRepository.Items.Add(CreateUser(active: true));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Wrong!123")));
        Assert.Empty(refreshTokenRepository.Items);
    }

    [Fact]
    public async Task ShouldThrowInvalidCredentialsExceptionWhenLoginDoesNotExist()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        userRepository.Items.Add(CreateUser(active: true));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("unknown", "S3cret!1")));
        Assert.Empty(refreshTokenRepository.Items);
    }

    [Fact]
    public async Task ShouldThrowInvalidCredentialsExceptionWhenCredentialsAreBlank()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        userRepository.Items.Add(CreateUser(active: true));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("   ", "")));
        Assert.Empty(refreshTokenRepository.Items);
    }
}
