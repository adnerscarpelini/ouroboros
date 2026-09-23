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

    [Fact]
    public async Task ShouldGenerateEmailConfirmationTokenWhenUserIsRegistered()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3cret!1"));

        Assert.Equal("raw-token", response.EmailConfirmationToken);
        Assert.Single(tokenRepository.Items);
        Assert.Equal(response.Id, tokenRepository.Items[0].UserExternalId);
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

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("jdoe", response.Login);
        Assert.Equal("John Doe", response.FullName);
        Assert.Equal("jdoe@example.com", response.Email);
        Assert.Single(repository.Items);
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
    public async Task ShouldThrowDomainExceptionWhenLoginOrEmailAlreadyExists()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator());

        await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3cret!1"));

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "Another Name", "other@example.com", "S3cret!1")));
        Assert.Single(repository.Items);
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
