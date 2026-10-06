namespace Ouroboros.Auth.Application.UseCases.ResetPassword;

using Ouroboros.Auth.Application.Fakes;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class ResetPasswordInteractorTests
{
    private const string CurrentPassword = "Current-Password-123";
    private const string NewPassword = "Brand-New-Password-456";
    private const string InvalidTokenMessage = "Invalid or expired password reset token";

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
            LockoutCleared.Add(externalId);
            return Task.CompletedTask;
        }
        public Task<bool> TryResetFailedAccessAsync(
            Guid externalId,
            DateTimeOffset now)
        {
            throw new NotSupportedException();
        }
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

        public Task<bool> ExistsDeletedByLoginAsync(string login)
        {
            return Task.FromResult(false);
        }

        public Task<int> CountActiveAdminsForUpdateAsync()
        {
            return Task.FromResult(0);
        }

        public List<Guid> LockoutCleared { get; } = new();

        // Simula a exclusao da conta entre a primeira leitura e a releitura dentro da transacao.
        public bool DeletedAfterFirstRead { get; set; }

        private int _reads;

        public Task<User?> GetByExternalIdAsync(Guid externalId)
        {
            _reads++;

            if (DeletedAfterFirstRead && _reads > 1)
            {
                return Task.FromResult<User?>(null);
            }

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
            Updated.Add(user);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenRepository : ITokenRepository
    {
        public List<Token> Items { get; } = new();

        public List<Token> MarkedAsUsed { get; } = new();

        public bool ConcurrentUse { get; set; }

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
            return Task.CompletedTask;
        }

        public Task InvalidatePendingByUserAsync(
            Guid userExternalId,
            TokenType type,
            DateTimeOffset invalidatedAt)
        {
            return Task.CompletedTask;
        }

        // Simula outra requisicao que usou o mesmo token antes desta gravar.
        public Task<int> DeleteExpiredBatchAsync(DateTimeOffset expiredBefore, int batchSize)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryMarkAsUsedAsync(Token token)
        {
            if (ConcurrentUse)
            {
                return Task.FromResult(false);
            }

            MarkedAsUsed.Add(token);
            return Task.FromResult(true);
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

        public bool FailOnRevokeAll { get; set; }

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
            if (FailOnRevokeAll)
            {
                throw new InvalidOperationException("Forced revocation failure.");
            }

            var activeTokens = Items.Where(item => item.UserExternalId == userExternalId && item.IsActive(revokedAt));

            foreach (var activeToken in activeTokens)
            {
                activeToken.Revoke(revokedAt);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string DummyHash => "hashed:dummy";
        public string Hash(string password)
        {
            return $"hashed:{password}";
        }

        public bool NeedsRehash(string passwordHash)
        {
            return false;
        }

        public bool Verify(string password, string passwordHash)
        {
            return passwordHash == Hash(password);
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

    private sealed class Scenario
    {
        public FakeUserRepository UserRepository { get; } = new();

        public FakeTokenRepository TokenRepository { get; } = new();

        public FakeRefreshTokenRepository RefreshTokenRepository { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public FakeAuditLog AuditLog { get; } = new();

        public FakeBreachedPasswordChecker BreachedPasswordChecker { get; } = new();

        public User User { get; }

        public DateTimeOffset OriginalPasswordChangedAt { get; }

        public Scenario(bool userActive = true)
        {
            OriginalPasswordChangedAt = DateTimeOffset.UtcNow.AddDays(-30);
            User = User.Rehydrate(
                1,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddDays(-60),
                null,
                "jdoe",
                "John Doe",
                "jdoe@example.com",
                true,
                $"hashed:{CurrentPassword}",
                OriginalPasswordChangedAt,
                userActive,
                null,
                UserRole.User,
                null);
            UserRepository.Items.Add(User);
        }

        public Token AddToken(
            TokenType type,
            DateTimeOffset expiresAt,
            DateTimeOffset? usedAt)
        {
            var token = Token.Rehydrate(
                1,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(-10),
                null,
                User.ExternalId,
                type,
                "hashed:raw-token",
                expiresAt,
                usedAt);
            TokenRepository.Items.Add(token);

            return token;
        }

        public Token AddPendingToken()
        {
            return AddToken(TokenType.PasswordReset, DateTimeOffset.UtcNow.AddMinutes(50), null);
        }

        public RefreshToken AddActiveRefreshToken()
        {
            var refreshToken = RefreshToken.Create(User.ExternalId, $"refresh-{Guid.NewGuid()}", DateTimeOffset.UtcNow.AddDays(7));
            RefreshTokenRepository.Items.Add(refreshToken);

            return refreshToken;
        }

        public ResetPasswordInteractor CreateInteractor()
        {
            return new ResetPasswordInteractor(
                TokenRepository,
                UserRepository,
                RefreshTokenRepository,
                new FakePasswordHasher(),
                new FakeTokenGenerator(),
                UnitOfWork,
                BreachedPasswordChecker,
                AuditLog);
        }

        public void AssertNothingChanged()
        {
            Assert.Empty(UserRepository.Updated);
            Assert.Empty(UserRepository.LockoutCleared);
            Assert.Empty(TokenRepository.MarkedAsUsed);
            Assert.Equal($"hashed:{CurrentPassword}", User.PasswordHash);
            Assert.All(RefreshTokenRepository.Items, item => Assert.Null(item.RevokedAt));
        }
    }

    [Fact]
    public async Task ShouldChangePasswordWhenTokenIsValid()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();

        var response = await scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword));

        Assert.Equal(scenario.User.ExternalId, response.UserId);
        var updatedUser = Assert.Single(scenario.UserRepository.Updated);
        Assert.Equal($"hashed:{NewPassword}", updatedUser.PasswordHash);
        Assert.True(updatedUser.PasswordChangedAt > scenario.OriginalPasswordChangedAt);
        Assert.NotNull(updatedUser.UpdatedAt);
    }

    [Fact]
    public async Task ShouldMarkTokenAsUsedWhenPasswordIsChanged()
    {
        var scenario = new Scenario();
        var token = scenario.AddPendingToken();

        await scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword));

        Assert.Same(token, Assert.Single(scenario.TokenRepository.MarkedAsUsed));
        Assert.NotNull(token.UsedAt);
    }

    [Fact]
    public async Task ShouldRevokeAllActiveRefreshTokensWhenPasswordIsChanged()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();
        scenario.AddActiveRefreshToken();

        await scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword));

        Assert.All(scenario.RefreshTokenRepository.Items, item => Assert.NotNull(item.RevokedAt));
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenDoesNotExist()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("unknown-token", NewPassword)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenIsBlank()
    {
        var scenario = new Scenario();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("   ", NewPassword)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenIsExpired()
    {
        var scenario = new Scenario();
        scenario.AddToken(TokenType.PasswordReset, DateTimeOffset.UtcNow.AddMinutes(-1), null);
        scenario.AddActiveRefreshToken();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenWasAlreadyUsed()
    {
        var scenario = new Scenario();
        scenario.AddToken(TokenType.PasswordReset, DateTimeOffset.UtcNow.AddMinutes(50), DateTimeOffset.UtcNow.AddMinutes(-5));
        scenario.AddActiveRefreshToken();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenIsEmailConfirmation()
    {
        var scenario = new Scenario();
        scenario.AddToken(TokenType.EmailConfirmation, DateTimeOffset.UtcNow.AddHours(20), null);
        scenario.AddActiveRefreshToken();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenIsUsedConcurrently()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();
        scenario.TokenRepository.ConcurrentUse = true;

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenUserIsInactive()
    {
        var scenario = new Scenario(userActive: false);
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();

        await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        scenario.AssertNothingChanged();
    }

    [Theory]
    [InlineData("Ab@1")]
    [InlineData("fourteen-chars")]
    [InlineData("1q2w3e4r5t6y7u8i")]
    [InlineData("my-jdoe-passphrase-ok")]
    public async Task ShouldThrowDomainExceptionWhenNewPasswordIsWeak(string weakPassword)
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();

        await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", weakPassword)));

        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionAndKeepTokenPendingWhenNewPasswordHasAppearedInADataBreach()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.BreachedPasswordChecker.BreachedPasswords.Add(NewPassword);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        Assert.Equal("Password is too common or has appeared in a data breach", exception.Message);
        Assert.Equal(0, scenario.UnitOfWork.Commits);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenNewPasswordEqualsCurrentPassword()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", CurrentPassword)));

        Assert.Equal("New password must be different from the current password", exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldKeepTokenPendingWhenNewPasswordIsRejected()
    {
        var scenario = new Scenario();
        var token = scenario.AddPendingToken();

        await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", "weak")));

        Assert.True(token.IsPending(DateTimeOffset.UtcNow));
    }

    // Spec 2026092504

    [Fact]
    public async Task ShouldClearLockoutWhenPasswordIsChanged()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();

        await scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword));

        Assert.Equal(scenario.User.ExternalId, Assert.Single(scenario.UserRepository.LockoutCleared));
    }

    [Fact]
    public async Task ShouldRunAllWritesInsideTheSameUnitOfWork()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();

        await scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword));

        Assert.Equal(1, scenario.UnitOfWork.Commits);
        Assert.Equal(0, scenario.UnitOfWork.Rollbacks);
    }

    [Theory]
    [InlineData("weak")]
    [InlineData(CurrentPassword)]
    public async Task ShouldNotOpenUnitOfWorkWhenNewPasswordIsRejected(string rejectedPassword)
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();

        await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", rejectedPassword)));

        Assert.Equal(0, scenario.UnitOfWork.Commits);
        Assert.Equal(0, scenario.UnitOfWork.Rollbacks);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldRollBackWhenSessionRevocationFails()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();
        scenario.RefreshTokenRepository.FailOnRevokeAll = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        Assert.Equal(0, scenario.UnitOfWork.Commits);
        Assert.Equal(1, scenario.UnitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldRollBackWithGenericMessageWhenAccountWasDeletedAfterTokenWasRead()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.UserRepository.DeletedAfterFirstRead = true;

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
        Assert.Empty(scenario.UserRepository.Updated);
        Assert.Empty(scenario.UserRepository.LockoutCleared);
        Assert.Equal(1, scenario.UnitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldRollBackAndKeepLockoutWhenTokenIsUsedConcurrently()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.TokenRepository.ConcurrentUse = true;

        await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        Assert.Equal(1, scenario.UnitOfWork.Rollbacks);
        Assert.Empty(scenario.UserRepository.LockoutCleared);
    }

    [Fact]
    public async Task ShouldRecordThePasswordResetCompletedEventInsideTheUnitOfWork()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();

        await scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword));

        var auditEvent = Assert.Single(scenario.AuditLog.Events);
        Assert.Equal(AuditEventType.PasswordResetCompleted, auditEvent.Type);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
        Assert.Equal(scenario.User.ExternalId, auditEvent.UserExternalId);
        Assert.Equal(1, scenario.UnitOfWork.Commits);
        var serialized = System.Text.Json.JsonSerializer.Serialize(auditEvent);
        Assert.DoesNotContain(NewPassword, serialized);
        Assert.DoesNotContain("raw-token", serialized);
    }

    [Fact]
    public async Task ShouldRecordNoEventWhenTheResetIsRejected()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();

        await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", "short")));

        Assert.Empty(scenario.AuditLog.Events);
    }

    [Fact]
    public async Task ShouldRollBackWhenTheAuditEventCannotBeRecorded()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.AuditLog.RecordException = new InvalidOperationException("audit failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        Assert.Equal(0, scenario.UnitOfWork.Commits);
        Assert.Equal(1, scenario.UnitOfWork.Rollbacks);
    }
}
