namespace Ouroboros.Auth.Application.UseCases.RegisterUser;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class RegisterUserInteractorTests
{
    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Items { get; } = new();

        public Task AddAsync(User user)
        {
            Items.Add(user);
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

        public Task RemoveAsync(Guid externalId)
        {
            Items.RemoveAll(item => item.ExternalId == externalId);
            return Task.CompletedTask;
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

    private sealed class FakeTokenRepository : ITokenRepository
    {
        public List<Token> Items { get; } = new();

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
            var exists = Items.Any(item => item.UserExternalId == userExternalId && item.Type == type && item.IsPending(now));
            return Task.FromResult(exists);
        }

        public Task UpdateAsync(Token token)
        {
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

    private static User CreateExistingUser(
        string login,
        string email)
    {
        return User.Create(login, "Existing User", email, "hashed:S3cret!1");
    }

    private static Token CreateConfirmationToken(
        Guid userExternalId,
        DateTimeOffset expiresAt)
    {
        return Token.Rehydrate(
            1,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(-2),
            null,
            userExternalId,
            TokenType.EmailConfirmation,
            "hashed:existing-token",
            expiresAt,
            null);
    }

    [Fact]
    public async Task ShouldRegisterUserWithUserRoleWhenUserIsRegistered()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator());

        await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3cret!1"));

        Assert.Equal(UserRole.User, userRepository.Items[0].Role);
    }

    [Fact]
    public async Task ShouldGenerateEmailConfirmationTokenWhenUserIsRegistered()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3cret!1"));

        Assert.Equal("raw-token", response.EmailConfirmationToken);
        Assert.Single(tokenRepository.Items);
        Assert.Equal(response.UserId, tokenRepository.Items[0].UserExternalId);
        Assert.Equal(TokenType.EmailConfirmation, tokenRepository.Items[0].Type);
        Assert.Equal("hashed:raw-token", tokenRepository.Items[0].TokenHash);
        Assert.True(tokenRepository.Items[0].ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Null(tokenRepository.Items[0].UsedAt);
    }

    [Fact]
    public async Task ShouldNotGenerateTokenWhenRegistrationIsRejected()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "not-an-email", "S3cret!1")));
        Assert.Empty(tokenRepository.Items);
    }

    [Fact]
    public async Task ShouldRegisterUserInactiveWhenDataIsValid()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3cret!1"));

        Assert.Single(repository.Items);
        Assert.Equal(repository.Items[0].ExternalId, response.UserId);
        Assert.Equal("jdoe", repository.Items[0].Login);
        Assert.Equal("John Doe", repository.Items[0].FullName);
        Assert.Equal("jdoe@example.com", repository.Items[0].Email);
        Assert.Equal("hashed:S3cret!1", repository.Items[0].PasswordHash);
        Assert.False(repository.Items[0].Active);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenLoginIsInvalid()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("   ", "John Doe", "jdoe@example.com", "S3cret!1")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenEmailIsInvalid()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "not-an-email", "S3cret!1")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenLoginBelongsToConfirmedUser()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var existing = CreateExistingUser("jdoe", "jdoe@example.com");
        existing.ConfirmEmail();
        userRepository.Items.Add(existing);
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator());

        var exception = await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "Another Name", "other@example.com", "S3cret!1")));

        Assert.Equal("Login already in use", exception.Message);
        Assert.Same(existing, Assert.Single(userRepository.Items));
        Assert.Empty(tokenRepository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenLoginBelongsToPendingRegistration()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var existing = CreateExistingUser("jdoe", "jdoe@example.com");
        userRepository.Items.Add(existing);
        tokenRepository.Items.Add(CreateConfirmationToken(existing.ExternalId, DateTimeOffset.UtcNow.AddHours(1)));
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "Another Name", "other@example.com", "S3cret!1")));

        Assert.Same(existing, Assert.Single(userRepository.Items));
        Assert.Single(tokenRepository.Items);
    }

    [Fact]
    public async Task ShouldReturnEmptyResponseWithoutCreatingUserWhenEmailBelongsToConfirmedUser()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var existing = CreateExistingUser("jdoe", "jdoe@example.com");
        existing.ConfirmEmail();
        userRepository.Items.Add(existing);
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("another", "Another Name", "jdoe@example.com", "S3cret!1"));

        Assert.Null(response.UserId);
        Assert.Null(response.EmailConfirmationToken);
        Assert.Same(existing, Assert.Single(userRepository.Items));
        Assert.Empty(tokenRepository.Items);
    }

    [Fact]
    public async Task ShouldKeepPendingRegistrationWhenEmailIsReusedWithinConfirmationWindow()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var existing = CreateExistingUser("jdoe", "jdoe@example.com");
        userRepository.Items.Add(existing);
        tokenRepository.Items.Add(CreateConfirmationToken(existing.ExternalId, DateTimeOffset.UtcNow.AddHours(1)));
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("another", "Another Name", "jdoe@example.com", "S3cret!1"));

        Assert.Null(response.UserId);
        Assert.Null(response.EmailConfirmationToken);
        Assert.Same(existing, Assert.Single(userRepository.Items));
        Assert.Single(tokenRepository.Items);
    }

    [Fact]
    public async Task ShouldReplaceAbandonedRegistrationWhenEmailIsReused()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var abandoned = CreateExistingUser("squatter", "jdoe@example.com");
        userRepository.Items.Add(abandoned);
        tokenRepository.Items.Add(CreateConfirmationToken(abandoned.ExternalId, DateTimeOffset.UtcNow.AddHours(-1)));
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3cret!1"));

        var user = Assert.Single(userRepository.Items);
        Assert.Equal("jdoe", user.Login);
        Assert.Equal(user.ExternalId, response.UserId);
        Assert.Equal("raw-token", response.EmailConfirmationToken);
    }

    [Fact]
    public async Task ShouldReplaceAbandonedRegistrationWhenLoginIsReused()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        userRepository.Items.Add(CreateExistingUser("jdoe", "old@example.com"));
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3cret!1"));

        var user = Assert.Single(userRepository.Items);
        Assert.Equal("jdoe@example.com", user.Email);
        Assert.Equal(user.ExternalId, response.UserId);
    }

    [Fact]
    public async Task ShouldRemoveBothAbandonedRegistrationsWhenLoginAndEmailBelongToDifferentAccounts()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        userRepository.Items.Add(CreateExistingUser("jdoe", "old@example.com"));
        userRepository.Items.Add(CreateExistingUser("squatter", "jdoe@example.com"));
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3cret!1"));

        var user = Assert.Single(userRepository.Items);
        Assert.Equal("jdoe", user.Login);
        Assert.Equal("jdoe@example.com", user.Email);
        Assert.Equal(user.ExternalId, response.UserId);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenPasswordIsTooShort()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3c!1")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenPasswordHasNoUppercaseLetter()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "s3cret!1")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenPasswordHasNoLowercaseLetter()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3CRET!1")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenPasswordHasNoDigit()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Secret!!")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenPasswordHasNoSpecialCharacter()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Secret123")));
        Assert.Empty(repository.Items);
    }
}
