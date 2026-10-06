namespace Ouroboros.Auth.Application.UseCases.RequestPasswordReset;

using Ouroboros.Auth.Application.Fakes;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class RequestPasswordResetInteractorTests
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

    private sealed class FakeTokenRepository : ITokenRepository
    {
        public List<Token> Items { get; } = new();

        public List<Token> Added { get; } = new();

        public Task AddAsync(Token token)
        {
            Items.Add(token);
            Added.Add(token);
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
            return Task.CompletedTask;
        }

        // Replica a semantica do SQL real: tokens pendentes do usuario e tipo passam a expirar em invalidatedAt.
        public Task InvalidatePendingByUserAsync(
            Guid userExternalId,
            TokenType type,
            DateTimeOffset invalidatedAt)
        {
            for (var index = 0; index < Items.Count; index++)
            {
                var token = Items[index];
                var isPending = token.UserExternalId == userExternalId
                    && token.Type == type
                    && token.UsedAt is null
                    && token.ExpiresAt > invalidatedAt;

                if (isPending)
                {
                    Items[index] = Token.Rehydrate(
                        token.Id,
                        token.ExternalId,
                        token.CreatedAt,
                        invalidatedAt,
                        token.UserExternalId,
                        token.Type,
                        token.TokenHash,
                        invalidatedAt,
                        token.UsedAt);
                }
            }

            return Task.CompletedTask;
        }

        public Task<int> DeleteExpiredBatchAsync(DateTimeOffset expiredBefore, int batchSize)
        {
            throw new NotSupportedException();
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

    private static User CreateActiveUser()
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed-password");
        user.ConfirmEmail();

        return user;
    }

    private static Token CreatePendingToken(Guid userExternalId, TokenType type)
    {
        return Token.Rehydrate(
            1,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-10),
            null,
            userExternalId,
            type,
            $"hashed:old-{type}",
            DateTimeOffset.UtcNow.AddMinutes(50),
            null);
    }

    private static RequestPasswordResetInteractor CreateInteractor(
        FakeUserRepository userRepository,
        FakeTokenRepository tokenRepository,
        FakeUnitOfWork? unitOfWork = null,
        FakeAuditLog? auditLog = null)
    {
        return new RequestPasswordResetInteractor(userRepository, tokenRepository, new FakeTokenGenerator(), unitOfWork ?? new FakeUnitOfWork(), auditLog ?? new FakeAuditLog());
    }

    [Fact]
    public async Task ShouldGeneratePasswordResetTokenWhenLoginBelongsToActiveUser()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var user = CreateActiveUser();
        userRepository.Items.Add(user);
        var interactor = CreateInteractor(userRepository, tokenRepository);

        var response = await interactor.ExecuteAsync(new RequestPasswordResetRequest("jdoe"));

        Assert.Equal(user.ExternalId, response.UserId);
        Assert.Equal("raw-token", response.PasswordResetToken);
        Assert.Single(tokenRepository.Added);
        Assert.Equal(user.ExternalId, tokenRepository.Added[0].UserExternalId);
        Assert.Equal("hashed:raw-token", tokenRepository.Added[0].TokenHash);
    }

    [Fact]
    public async Task ShouldGeneratePasswordResetTokenWhenEmailBelongsToActiveUser()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var user = CreateActiveUser();
        userRepository.Items.Add(user);
        var interactor = CreateInteractor(userRepository, tokenRepository);

        var response = await interactor.ExecuteAsync(new RequestPasswordResetRequest("  jdoe@example.com  "));

        Assert.Equal(user.ExternalId, response.UserId);
        Assert.Equal("raw-token", response.PasswordResetToken);
        Assert.Single(tokenRepository.Added);
    }

    [Fact]
    public async Task ShouldCreateTokenWithPasswordResetTypeAndOneHourExpirationWhenUserIsActive()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        userRepository.Items.Add(CreateActiveUser());
        var interactor = CreateInteractor(userRepository, tokenRepository);
        var before = DateTimeOffset.UtcNow;

        await interactor.ExecuteAsync(new RequestPasswordResetRequest("jdoe"));

        var after = DateTimeOffset.UtcNow;
        var token = Assert.Single(tokenRepository.Added);
        Assert.Equal(TokenType.PasswordReset, token.Type);
        Assert.Null(token.UsedAt);
        Assert.InRange(token.ExpiresAt, before.AddHours(1), after.AddHours(1));
    }

    [Fact]
    public async Task ShouldReturnWithoutTokenWhenUserDoesNotExist()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        userRepository.Items.Add(CreateActiveUser());
        var interactor = CreateInteractor(userRepository, tokenRepository);

        var response = await interactor.ExecuteAsync(new RequestPasswordResetRequest("unknown"));

        Assert.Null(response.UserId);
        Assert.Null(response.PasswordResetToken);
        Assert.Empty(tokenRepository.Added);
    }

    [Fact]
    public async Task ShouldReturnWithoutTokenWhenUserIsInactiveWithUnconfirmedEmail()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        userRepository.Items.Add(User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed-password"));
        var interactor = CreateInteractor(userRepository, tokenRepository);

        var response = await interactor.ExecuteAsync(new RequestPasswordResetRequest("jdoe"));

        Assert.Null(response.UserId);
        Assert.Null(response.PasswordResetToken);
        Assert.Empty(tokenRepository.Added);
    }

    [Fact]
    public async Task ShouldReturnWithoutTokenWhenUserIsInactiveWithConfirmedEmail()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        userRepository.Items.Add(User.Rehydrate(
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
            false,
            null,
            UserRole.User,
            null));
        var interactor = CreateInteractor(userRepository, tokenRepository);

        var response = await interactor.ExecuteAsync(new RequestPasswordResetRequest("jdoe"));

        Assert.Null(response.PasswordResetToken);
        Assert.Empty(tokenRepository.Added);
    }

    [Fact]
    public async Task ShouldInvalidatePreviousPendingPasswordResetTokensWhenNewTokenIsRequested()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var user = CreateActiveUser();
        userRepository.Items.Add(user);
        tokenRepository.Items.Add(CreatePendingToken(user.ExternalId, TokenType.PasswordReset));
        var interactor = CreateInteractor(userRepository, tokenRepository);

        await interactor.ExecuteAsync(new RequestPasswordResetRequest("jdoe"));

        var previousToken = tokenRepository.Items.Single(item => item.TokenHash == "hashed:old-PasswordReset");
        var newToken = tokenRepository.Items.Single(item => item.TokenHash == "hashed:raw-token");
        Assert.True(previousToken.ExpiresAt <= DateTimeOffset.UtcNow);
        Assert.True(newToken.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ShouldKeepEmailConfirmationTokensWhenPasswordResetIsRequested()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var user = CreateActiveUser();
        userRepository.Items.Add(user);
        var emailConfirmationToken = CreatePendingToken(user.ExternalId, TokenType.EmailConfirmation);
        tokenRepository.Items.Add(emailConfirmationToken);
        var interactor = CreateInteractor(userRepository, tokenRepository);

        await interactor.ExecuteAsync(new RequestPasswordResetRequest("jdoe"));

        Assert.Contains(emailConfirmationToken, tokenRepository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenLoginOrEmailIsBlank()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var interactor = CreateInteractor(userRepository, tokenRepository);

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RequestPasswordResetRequest("   ")));
        Assert.Empty(tokenRepository.Added);
    }

    [Fact]
    public async Task ShouldRecordThePasswordResetRequestedEventInsideTheUnitOfWorkWhenTheAccountExists()
    {
        var userRepository = new FakeUserRepository();
        var user = CreateActiveUser();
        userRepository.Items.Add(user);
        var unitOfWork = new FakeUnitOfWork();
        var auditLog = new FakeAuditLog();
        var interactor = CreateInteractor(userRepository, new FakeTokenRepository(), unitOfWork, auditLog);

        await interactor.ExecuteAsync(new RequestPasswordResetRequest("jdoe"));

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal(AuditEventType.PasswordResetRequested, auditEvent.Type);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
        Assert.Equal(user.ExternalId, auditEvent.UserExternalId);
        Assert.Equal(1, unitOfWork.Commits);
        Assert.DoesNotContain("raw-token", System.Text.Json.JsonSerializer.Serialize(auditEvent));
    }

    [Fact]
    public async Task ShouldRecordNoEventWhenTheAccountDoesNotExist()
    {
        var auditLog = new FakeAuditLog();
        var interactor = CreateInteractor(new FakeUserRepository(), new FakeTokenRepository(), auditLog: auditLog);

        await interactor.ExecuteAsync(new RequestPasswordResetRequest("unknown"));

        Assert.Empty(auditLog.Events);
    }

    [Fact]
    public async Task ShouldRollBackWhenTheAuditEventCannotBeRecorded()
    {
        var userRepository = new FakeUserRepository();
        userRepository.Items.Add(CreateActiveUser());
        var unitOfWork = new FakeUnitOfWork();
        var interactor = CreateInteractor(
            userRepository,
            new FakeTokenRepository(),
            unitOfWork,
            new FakeAuditLog { RecordException = new InvalidOperationException("audit failed") });

        await Assert.ThrowsAsync<InvalidOperationException>(() => interactor.ExecuteAsync(new RequestPasswordResetRequest("jdoe")));

        Assert.Equal(0, unitOfWork.Commits);
        Assert.Equal(1, unitOfWork.Rollbacks);
    }
}
