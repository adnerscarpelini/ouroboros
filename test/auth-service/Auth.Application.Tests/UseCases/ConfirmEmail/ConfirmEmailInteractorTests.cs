namespace Ouroboros.Auth.Application.UseCases.ConfirmEmail;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class ConfirmEmailInteractorTests
{
    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Items { get; } = new();

        public List<User> Updated { get; } = new();

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
            Updated.Add(user);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenRepository : ITokenRepository
    {
        public List<Token> Items { get; } = new();

        public List<Token> Updated { get; } = new();

        public Task AddAsync(Token token)
        {
            Items.Add(token);
            return Task.CompletedTask;
        }

        public Task<Token?> GetByHashAsync(string tokenHash, TokenType type)
        {
            var token = Items.FirstOrDefault(item => item.TokenHash == tokenHash && item.Type == type);
            return Task.FromResult(token);
        }

        public Task<bool> ExistsPendingByUserAsync(
            Guid userExternalId,
            TokenType type,
            DateTimeOffset now)
        {
            return Task.FromResult(false);
        }

        public Task UpdateAsync(Token token)
        {
            Updated.Add(token);
            return Task.CompletedTask;
        }

        public Task InvalidatePendingByUserAsync(
            Guid userExternalId,
            TokenType type,
            DateTimeOffset invalidatedAt)
        {
            return Task.CompletedTask;
        }

        public Task<bool> TryMarkAsUsedAsync(Token token)
        {
            Updated.Add(token);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeTokenGenerator : ITokenGenerator
    {
        public string Generate()
        {
            return "raw-token";
        }

        public string Hash(string token)
        {
            return $"hashed:{token}";
        }
    }

    private static User CreateUser()
    {
        return User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed-password");
    }

    private static Token CreateToken(
        Guid userExternalId,
        DateTimeOffset expiresAt,
        DateTimeOffset? usedAt)
    {
        return Token.Rehydrate(
            1,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(-2),
            null,
            userExternalId,
            TokenType.EmailConfirmation,
            "hashed:raw-token",
            expiresAt,
            usedAt);
    }

    [Fact]
    public async Task ShouldConfirmEmailAndActivateUserWhenTokenIsValid()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var user = CreateUser();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreateToken(user.ExternalId, DateTimeOffset.UtcNow.AddHours(1), null));
        var interactor = new ConfirmEmailInteractor(tokenRepository, userRepository, new FakeTokenGenerator());

        var response = await interactor.ExecuteAsync(new ConfirmEmailRequest("raw-token"));

        Assert.Equal(user.ExternalId, response.UserId);
        Assert.Equal("jdoe", response.Login);
        Assert.Equal("jdoe@example.com", response.Email);
        Assert.Single(userRepository.Updated);
        Assert.True(userRepository.Updated[0].EmailConfirmed);
        Assert.True(userRepository.Updated[0].Active);
        Assert.NotNull(userRepository.Updated[0].UpdatedAt);
        Assert.Single(tokenRepository.Updated);
        Assert.NotNull(tokenRepository.Updated[0].UsedAt);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenIsExpired()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var user = CreateUser();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreateToken(user.ExternalId, DateTimeOffset.UtcNow.AddMinutes(-1), null));
        var interactor = new ConfirmEmailInteractor(tokenRepository, userRepository, new FakeTokenGenerator());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new ConfirmEmailRequest("raw-token")));
        Assert.Empty(userRepository.Updated);
        Assert.Empty(tokenRepository.Updated);
        Assert.False(user.Active);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenWasAlreadyUsed()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var user = CreateUser();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreateToken(user.ExternalId, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddMinutes(-5)));
        var interactor = new ConfirmEmailInteractor(tokenRepository, userRepository, new FakeTokenGenerator());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new ConfirmEmailRequest("raw-token")));
        Assert.Empty(userRepository.Updated);
        Assert.Empty(tokenRepository.Updated);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenDoesNotExist()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var user = CreateUser();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreateToken(user.ExternalId, DateTimeOffset.UtcNow.AddHours(1), null));
        var interactor = new ConfirmEmailInteractor(tokenRepository, userRepository, new FakeTokenGenerator());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new ConfirmEmailRequest("unknown-token")));
        Assert.Empty(userRepository.Updated);
        Assert.Empty(tokenRepository.Updated);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenIsBlank()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var interactor = new ConfirmEmailInteractor(tokenRepository, userRepository, new FakeTokenGenerator());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new ConfirmEmailRequest("   ")));
        Assert.Empty(userRepository.Updated);
        Assert.Empty(tokenRepository.Updated);
    }
}
