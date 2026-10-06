namespace Ouroboros.Auth.Application.UseCases.ConfirmEmail;

using Ouroboros.Auth.Application.Fakes;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class ConfirmEmailInteractorTests
{
    private const string InvalidTokenMessage = "Invalid or expired confirmation token";

    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<bool> RecordFailedAccessAsync(
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

        public List<User> Updated { get; } = new();

        public bool FailOnUpdate { get; set; }

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

        public Task<int> CountActiveAdminsAsync()
        {
            return Task.FromResult(0);
        }

        // Como o repositorio real, ignora conta excluida: pra aplicacao ela nao existe.
        public Task<User?> GetByExternalIdAsync(Guid externalId)
        {
            var user = Items.FirstOrDefault(item => item.ExternalId == externalId && item.DeletedAt is null);
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
            if (FailOnUpdate)
            {
                throw new InvalidOperationException("Forced user update failure.");
            }

            Updated.Add(user);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenRepository : ITokenRepository
    {
        public List<Token> Items { get; } = new();

        public List<Token> Updated { get; } = new();

        // Simula o UPDATE condicional: false quando outra requisicao consumiu o token antes.
        public bool TryMarkAsUsedResult { get; set; } = true;

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
            if (TryMarkAsUsedResult)
            {
                Updated.Add(token);
            }

            return Task.FromResult(TryMarkAsUsedResult);
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
        DateTimeOffset? usedAt,
        TokenType type = TokenType.EmailConfirmation)
    {
        return Token.Rehydrate(
            1,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(-2),
            null,
            userExternalId,
            type,
            "hashed:raw-token",
            expiresAt,
            usedAt);
    }

    private static ConfirmEmailInteractor CreateInteractor(
        FakeTokenRepository tokenRepository,
        FakeUserRepository userRepository,
        FakeUnitOfWork unitOfWork)
    {
        return new ConfirmEmailInteractor(tokenRepository, userRepository, new FakeTokenGenerator(), unitOfWork);
    }

    [Fact]
    public async Task ShouldConfirmEmailAndActivateUserWhenTokenIsValid()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var user = CreateUser();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreateToken(user.ExternalId, DateTimeOffset.UtcNow.AddHours(1), null));
        var interactor = CreateInteractor(tokenRepository, userRepository, unitOfWork);

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
    public async Task ShouldConsumeTokenAndActivateUserInsideTheSameUnitOfWork()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var user = CreateUser();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreateToken(user.ExternalId, DateTimeOffset.UtcNow.AddHours(1), null));
        var interactor = CreateInteractor(tokenRepository, userRepository, unitOfWork);

        await interactor.ExecuteAsync(new ConfirmEmailRequest("raw-token"));

        Assert.Equal(1, unitOfWork.Commits);
        Assert.Equal(0, unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldThrowGenericMessageWhenTokenIsExpired()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var user = CreateUser();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreateToken(user.ExternalId, DateTimeOffset.UtcNow.AddMinutes(-1), null));
        var interactor = CreateInteractor(tokenRepository, userRepository, new FakeUnitOfWork());

        await AssertInvalidTokenAsync(interactor, "raw-token");

        Assert.Empty(userRepository.Updated);
        Assert.Empty(tokenRepository.Updated);
        Assert.False(user.Active);
    }

    [Fact]
    public async Task ShouldThrowGenericMessageWhenTokenWasAlreadyUsed()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var user = CreateUser();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreateToken(user.ExternalId, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddMinutes(-5)));
        var interactor = CreateInteractor(tokenRepository, userRepository, new FakeUnitOfWork());

        await AssertInvalidTokenAsync(interactor, "raw-token");

        Assert.Empty(userRepository.Updated);
        Assert.Empty(tokenRepository.Updated);
    }

    [Fact]
    public async Task ShouldThrowGenericMessageWhenTokenIsOfAnotherType()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var user = CreateUser();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreateToken(user.ExternalId, DateTimeOffset.UtcNow.AddHours(1), null, TokenType.PasswordReset));
        var interactor = CreateInteractor(tokenRepository, userRepository, new FakeUnitOfWork());

        await AssertInvalidTokenAsync(interactor, "raw-token");

        Assert.Empty(userRepository.Updated);
        Assert.Empty(tokenRepository.Updated);
        Assert.False(user.Active);
    }

    [Fact]
    public async Task ShouldThrowGenericMessageWhenTokenDoesNotExist()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var user = CreateUser();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreateToken(user.ExternalId, DateTimeOffset.UtcNow.AddHours(1), null));
        var interactor = CreateInteractor(tokenRepository, userRepository, new FakeUnitOfWork());

        await AssertInvalidTokenAsync(interactor, "unknown-token");

        Assert.Empty(userRepository.Updated);
        Assert.Empty(tokenRepository.Updated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ShouldThrowGenericMessageWhenTokenIsBlank(string token)
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var interactor = CreateInteractor(tokenRepository, userRepository, new FakeUnitOfWork());

        await AssertInvalidTokenAsync(interactor, token);

        Assert.Empty(userRepository.Updated);
        Assert.Empty(tokenRepository.Updated);
    }

    [Fact]
    public async Task ShouldThrowGenericMessageWhenUserDoesNotExist()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        tokenRepository.Items.Add(CreateToken(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(1), null));
        var interactor = CreateInteractor(tokenRepository, userRepository, unitOfWork);

        await AssertInvalidTokenAsync(interactor, "raw-token");

        Assert.Empty(userRepository.Updated);
        Assert.Equal(1, unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldThrowGenericMessageWhenUserWasDeleted()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var user = CreateUser();
        user.Delete();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreateToken(user.ExternalId, DateTimeOffset.UtcNow.AddHours(1), null));
        var interactor = CreateInteractor(tokenRepository, userRepository, unitOfWork);

        await AssertInvalidTokenAsync(interactor, "raw-token");

        Assert.Empty(userRepository.Updated);
        Assert.False(user.Active);
        Assert.Equal(1, unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldThrowGenericMessageAndKeepUserInactiveWhenAnotherRequestConsumedTheToken()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository { TryMarkAsUsedResult = false };
        var unitOfWork = new FakeUnitOfWork();
        var user = CreateUser();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreateToken(user.ExternalId, DateTimeOffset.UtcNow.AddHours(1), null));
        var interactor = CreateInteractor(tokenRepository, userRepository, unitOfWork);

        await AssertInvalidTokenAsync(interactor, "raw-token");

        Assert.Empty(userRepository.Updated);
        Assert.False(user.Active);
        Assert.Equal(1, unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldRollBackWhenUserUpdateFails()
    {
        var userRepository = new FakeUserRepository { FailOnUpdate = true };
        var tokenRepository = new FakeTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var user = CreateUser();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreateToken(user.ExternalId, DateTimeOffset.UtcNow.AddHours(1), null));
        var interactor = CreateInteractor(tokenRepository, userRepository, unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() => interactor.ExecuteAsync(new ConfirmEmailRequest("raw-token")));

        Assert.Equal(0, unitOfWork.Commits);
        Assert.Equal(1, unitOfWork.Rollbacks);
    }

    private static async Task AssertInvalidTokenAsync(ConfirmEmailInteractor interactor, string token)
    {
        var exception = await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new ConfirmEmailRequest(token)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
    }
}
