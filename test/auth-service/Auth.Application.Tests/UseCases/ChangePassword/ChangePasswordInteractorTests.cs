namespace Ouroboros.Auth.Application.UseCases.ChangePassword;

using Ouroboros.Auth.Application.Fakes;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

// Spec 2026092517: troca de senha autenticada, com reautenticacao e preservacao da sessao atual.
public class ChangePasswordInteractorTests
{
    private const string CurrentPassword = "Current-Password-1234";
    private const string NewPassword = "Brand-New-Password-5678";

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Items { get; } = new();

        public List<Guid> FailedAccess { get; } = new();

        public List<Guid> LockoutsCleared { get; } = new();

        public int Updates { get; private set; }

        public Exception? UpdateException { get; set; }

        public Task<User?> GetByExternalIdAsync(Guid externalId)
        {
            return Task.FromResult(Items.FirstOrDefault(item => item.DeletedAt is null && item.ExternalId == externalId));
        }

        public Task UpdateAsync(User user)
        {
            if (UpdateException is not null)
            {
                throw UpdateException;
            }

            Updates++;
            return Task.CompletedTask;
        }

        public bool LockOutOnNextFailure { get; set; }

        public Task<bool> RecordFailedAccessAsync(
            Guid externalId,
            DateTimeOffset now)
        {
            FailedAccess.Add(externalId);
            return Task.FromResult(LockOutOnNextFailure);
        }

        public Task ClearLockoutAsync(
            Guid externalId,
            DateTimeOffset now)
        {
            LockoutsCleared.Add(externalId);
            return Task.CompletedTask;
        }

        public Task AddAsync(User user) => throw new NotSupportedException();

        public Task<User?> GetByEmailAsync(string email) => throw new NotSupportedException();

        public Task<User?> GetByLoginAsync(string login) => throw new NotSupportedException();

        public Task<User?> GetByLoginOrEmailAsync(string loginOrEmail) => throw new NotSupportedException();

        public Task<bool> TryResetFailedAccessAsync(
            Guid externalId,
            DateTimeOffset now) => throw new NotSupportedException();

        public Task<bool> TryRegisterLoginAsync(
            Guid externalId,
            DateTimeOffset now) => throw new NotSupportedException();

        public Task<bool> TryRehashPasswordAsync(
            Guid externalId,
            string currentPasswordHash,
            string newPasswordHash) => throw new NotSupportedException();

        public Task RemoveAsync(Guid externalId) => throw new NotSupportedException();

        public Task<bool> ExistsDeletedByLoginAsync(string login) => throw new NotSupportedException();

        public Task<int> CountActiveAdminsForUpdateAsync() => throw new NotSupportedException();
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Items { get; } = new();

        public Exception? RevokeException { get; set; }

        public Task RevokeAllActiveByUserExceptSessionAsync(
            Guid userExternalId,
            Guid exceptSessionId,
            DateTimeOffset revokedAt)
        {
            if (RevokeException is not null)
            {
                throw RevokeException;
            }

            foreach (var token in Items.Where(item => item.UserExternalId == userExternalId && item.SessionId != exceptSessionId && item.IsActive(revokedAt)))
            {
                token.Revoke(revokedAt);
            }

            return Task.CompletedTask;
        }

        public Task<int> DeleteExpiredBatchAsync(
            DateTimeOffset expiredBefore,
            int batchSize) => throw new NotSupportedException();

        public Task AddAsync(RefreshToken refreshToken) => throw new NotSupportedException();

        public Task<RefreshToken?> GetByHashAsync(string tokenHash) => throw new NotSupportedException();

        public Task<bool> TryRevokeAsync(RefreshToken refreshToken) => throw new NotSupportedException();

        public Task RevokeAllActiveByUserAsync(
            Guid userExternalId,
            DateTimeOffset revokedAt) => throw new NotSupportedException();

        public Task RevokeAllActiveBySessionAsync(
            Guid sessionId,
            DateTimeOffset revokedAt) => throw new NotSupportedException();
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string DummyHash => "hashed:dummy";

        public List<string> VerifiedHashes { get; } = new();

        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(
            string password,
            string passwordHash)
        {
            VerifiedHashes.Add(passwordHash);
            return passwordHash == Hash(password);
        }

        public bool NeedsRehash(string passwordHash) => false;
    }

    private sealed class Scenario
    {
        public FakeUserRepository Users { get; } = new();

        public FakeRefreshTokenRepository RefreshTokens { get; } = new();

        public FakePasswordHasher Hasher { get; } = new();

        public FakeBreachedPasswordChecker BreachedPasswordChecker { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public FakeAuditLog AuditLog { get; } = new();

        public User User { get; }

        public RefreshToken CurrentSession { get; }

        public RefreshToken OtherSession { get; }

        public Scenario(bool active = true)
        {
            User = User.Create("jdoe", "John Doe", "jdoe@example.com", $"hashed:{CurrentPassword}");

            if (active)
            {
                User.ConfirmEmail();
            }

            Users.Items.Add(User);
            CurrentSession = RefreshToken.Create(User.ExternalId, "hash-current", DateTimeOffset.UtcNow.AddDays(1));
            OtherSession = RefreshToken.Create(User.ExternalId, "hash-other", DateTimeOffset.UtcNow.AddDays(1));
            RefreshTokens.Items.AddRange([CurrentSession, OtherSession]);
        }

        public ChangePasswordInteractor CreateInteractor()
        {
            return new ChangePasswordInteractor(Users, RefreshTokens, Hasher, BreachedPasswordChecker, UnitOfWork, AuditLog);
        }

        public ChangePasswordRequest Request(
            string currentPassword = CurrentPassword,
            string newPassword = NewPassword,
            bool withSession = true)
        {
            return new ChangePasswordRequest(User.ExternalId, withSession ? CurrentSession.SessionId : null, currentPassword, newPassword);
        }

        public void AssertNothingChanged()
        {
            Assert.Equal($"hashed:{CurrentPassword}", User.PasswordHash);
            Assert.Equal(0, Users.Updates);
            Assert.Null(CurrentSession.RevokedAt);
            Assert.Null(OtherSession.RevokedAt);
            Assert.Equal(0, UnitOfWork.Commits);
        }
    }

    [Fact]
    public async Task ShouldChangePasswordRevokeOtherSessionsAndKeepTheCurrentOneWhenCredentialsAreValid()
    {
        var scenario = new Scenario();

        var response = await scenario.CreateInteractor().ExecuteAsync(scenario.Request());

        Assert.Equal(scenario.User.ExternalId, response.UserId);
        Assert.Equal($"hashed:{NewPassword}", scenario.User.PasswordHash);
        Assert.Equal(1, scenario.Users.Updates);
        Assert.Null(scenario.CurrentSession.RevokedAt);
        Assert.NotNull(scenario.OtherSession.RevokedAt);
    }

    [Fact]
    public async Task ShouldRevokeEverySessionWhenTheAccessTokenHasNoSessionId()
    {
        var scenario = new Scenario();

        await scenario.CreateInteractor().ExecuteAsync(scenario.Request(withSession: false));

        Assert.NotNull(scenario.CurrentSession.RevokedAt);
        Assert.NotNull(scenario.OtherSession.RevokedAt);
    }

    [Fact]
    public async Task ShouldRunAllWritesInsideTheSameUnitOfWorkAndClearTheLockout()
    {
        var scenario = new Scenario();

        await scenario.CreateInteractor().ExecuteAsync(scenario.Request());

        Assert.Equal(1, scenario.UnitOfWork.Commits);
        Assert.Equal(0, scenario.UnitOfWork.Rollbacks);
        Assert.Equal(scenario.User.ExternalId, Assert.Single(scenario.Users.LockoutsCleared));
    }

    [Fact]
    public async Task ShouldRollBackWhenRevokingTheOtherSessionsFails()
    {
        var scenario = new Scenario();
        scenario.RefreshTokens.RevokeException = new InvalidOperationException("revoke failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.CreateInteractor().ExecuteAsync(scenario.Request()));

        Assert.Equal(0, scenario.UnitOfWork.Commits);
        Assert.Equal(1, scenario.UnitOfWork.Rollbacks);
        Assert.Null(scenario.OtherSession.RevokedAt);
    }

    [Fact]
    public async Task ShouldThrowInvalidCredentialsAndCountTheFailureWhenCurrentPasswordIsWrong()
    {
        var scenario = new Scenario();

        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            scenario.CreateInteractor().ExecuteAsync(scenario.Request(currentPassword: "Wrong-Password-0000")));

        Assert.Equal("Invalid login or password", exception.Message);
        Assert.Equal(scenario.User.ExternalId, Assert.Single(scenario.Users.FailedAccess));
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowInvalidCredentialsWithoutCountingWhenAccountIsLockedOut()
    {
        var scenario = new Scenario();

        for (var attempt = 0; attempt < User.MaxFailedAccessAttempts; attempt++)
        {
            scenario.User.RecordFailedAccess(DateTimeOffset.UtcNow);
        }

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => scenario.CreateInteractor().ExecuteAsync(scenario.Request()));

        Assert.Empty(scenario.Users.FailedAccess);
        Assert.Equal(scenario.Hasher.DummyHash, Assert.Single(scenario.Hasher.VerifiedHashes));
        scenario.AssertNothingChanged();
    }

    [Theory]
    [InlineData("fourteen-chars")]
    [InlineData("1q2w3e4r5t6y7u8i")]
    [InlineData("my-jdoe-passphrase-ok")]
    public async Task ShouldRejectNewPasswordThatBreaksThePolicyWithoutCountingAsAFailure(string newPassword)
    {
        var scenario = new Scenario();

        await Assert.ThrowsAsync<DomainException>(() => scenario.CreateInteractor().ExecuteAsync(scenario.Request(newPassword: newPassword)));

        Assert.Empty(scenario.Users.FailedAccess);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldRejectNewPasswordThatHasAppearedInADataBreach()
    {
        var scenario = new Scenario();
        scenario.BreachedPasswordChecker.BreachedPasswords.Add(NewPassword);

        var exception = await Assert.ThrowsAsync<DomainException>(() => scenario.CreateInteractor().ExecuteAsync(scenario.Request()));

        Assert.Equal("Password is too common or has appeared in a data breach", exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldRejectNewPasswordEqualToTheCurrentOne()
    {
        var scenario = new Scenario();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(scenario.Request(newPassword: CurrentPassword)));

        Assert.Equal("New password must be different from the current password", exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowInvalidAccessTokenWhenUserDoesNotExist()
    {
        var scenario = new Scenario();
        var request = new ChangePasswordRequest(Guid.NewGuid(), null, CurrentPassword, NewPassword);

        await Assert.ThrowsAsync<InvalidAccessTokenException>(() => scenario.CreateInteractor().ExecuteAsync(request));

        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowInvalidAccessTokenWhenUserWasDeleted()
    {
        var scenario = new Scenario();
        scenario.User.Delete();

        await Assert.ThrowsAsync<InvalidAccessTokenException>(() => scenario.CreateInteractor().ExecuteAsync(scenario.Request()));

        Assert.Equal(0, scenario.Users.Updates);
    }

    [Fact]
    public async Task ShouldThrowInvalidAccessTokenWhenUserIsNotActive()
    {
        var scenario = new Scenario(active: false);

        await Assert.ThrowsAsync<InvalidAccessTokenException>(() => scenario.CreateInteractor().ExecuteAsync(scenario.Request()));

        Assert.Empty(scenario.Users.FailedAccess);
    }

    [Fact]
    public async Task ShouldNotChangeAnyOtherUsersPassword()
    {
        var scenario = new Scenario();
        var other = User.Create("other", "Other User", "other@example.com", "hashed:Other-Password-1234");
        other.ConfirmEmail();
        scenario.Users.Items.Add(other);

        await scenario.CreateInteractor().ExecuteAsync(scenario.Request());

        Assert.Equal("hashed:Other-Password-1234", other.PasswordHash);
    }

    [Fact]
    public async Task ShouldRecordThePasswordChangedEventWithTheSessionInsideTheUnitOfWork()
    {
        var scenario = new Scenario();

        await scenario.CreateInteractor().ExecuteAsync(scenario.Request());

        var auditEvent = Assert.Single(scenario.AuditLog.Events);
        Assert.Equal(AuditEventType.PasswordChanged, auditEvent.Type);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
        Assert.Equal(scenario.User.ExternalId, auditEvent.UserExternalId);
        Assert.Equal(scenario.CurrentSession.SessionId, auditEvent.SessionId);
        Assert.Equal(1, scenario.UnitOfWork.Commits);
        var serialized = System.Text.Json.JsonSerializer.Serialize(auditEvent);
        Assert.DoesNotContain(CurrentPassword, serialized);
        Assert.DoesNotContain(NewPassword, serialized);
    }

    [Fact]
    public async Task ShouldRecordNoEventWhenTheCurrentPasswordIsWrongAndTheAccountIsNotLockedYet()
    {
        var scenario = new Scenario();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            scenario.CreateInteractor().ExecuteAsync(scenario.Request(currentPassword: "Wrong-Password-0000")));

        Assert.Empty(scenario.AuditLog.Events);
    }

    [Fact]
    public async Task ShouldRecordTheAccountLockedOutEventWhenTheWrongPasswordLocksTheAccount()
    {
        var scenario = new Scenario();
        scenario.Users.LockOutOnNextFailure = true;

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            scenario.CreateInteractor().ExecuteAsync(scenario.Request(currentPassword: "Wrong-Password-0000")));

        var auditEvent = Assert.Single(scenario.AuditLog.Events);
        Assert.Equal(AuditEventType.AccountLockedOut, auditEvent.Type);
        Assert.Equal(AuditOutcome.Failure, auditEvent.Outcome);
        Assert.Equal("too_many_failed_attempts", auditEvent.Reason);
    }

    [Fact]
    public async Task ShouldRollBackWhenThePasswordChangedEventCannotBeRecorded()
    {
        var scenario = new Scenario();
        scenario.AuditLog.RecordException = new InvalidOperationException("audit failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.CreateInteractor().ExecuteAsync(scenario.Request()));

        Assert.Equal(0, scenario.UnitOfWork.Commits);
        Assert.Equal(1, scenario.UnitOfWork.Rollbacks);
    }
}
