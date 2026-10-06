namespace Ouroboros.Auth.Application.UseCases.LogoutAll;

using Ouroboros.Auth.Application.Fakes;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Xunit;

public class LogoutAllInteractorTests
{
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
            return Task.FromResult(Items.FirstOrDefault(item => item.TokenHash == tokenHash));
        }

        public Task<bool> TryRevokeAsync(RefreshToken refreshToken)
        {
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

        public Task RevokeAllActiveByUserAsync(
            Guid userExternalId,
            DateTimeOffset revokedAt)
        {
            foreach (var token in Items.Where(item => item.UserExternalId == userExternalId && item.IsActive(revokedAt)))
            {
                token.Revoke(revokedAt);
            }

            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ShouldRevokeEveryActiveSessionOfTheUserWhenLoggingOutOfAll()
    {
        var repository = new FakeRefreshTokenRepository();
        var userId = Guid.NewGuid();
        var first = RefreshToken.Create(userId, "hash-1", DateTimeOffset.UtcNow.AddDays(1));
        var second = RefreshToken.Create(userId, "hash-2", DateTimeOffset.UtcNow.AddDays(1));
        repository.Items.AddRange([first, second]);
        var interactor = new LogoutAllInteractor(repository, new FakeUnitOfWork(), new FakeAuditLog());

        var response = await interactor.ExecuteAsync(new LogoutAllRequest(userId));

        Assert.Equal(userId, response.UserId);
        Assert.NotNull(first.RevokedAt);
        Assert.NotNull(second.RevokedAt);
    }

    [Fact]
    public async Task ShouldNotTouchSessionsOfOtherUsersWhenLoggingOutOfAll()
    {
        var repository = new FakeRefreshTokenRepository();
        var other = RefreshToken.Create(Guid.NewGuid(), "hash-other", DateTimeOffset.UtcNow.AddDays(1));
        repository.Items.Add(other);
        var interactor = new LogoutAllInteractor(repository, new FakeUnitOfWork(), new FakeAuditLog());

        await interactor.ExecuteAsync(new LogoutAllRequest(Guid.NewGuid()));

        Assert.Null(other.RevokedAt);
    }

    [Fact]
    public async Task ShouldCompleteWhenUserHasNoActiveSession()
    {
        var interactor = new LogoutAllInteractor(new FakeRefreshTokenRepository(), new FakeUnitOfWork(), new FakeAuditLog());

        var response = await interactor.ExecuteAsync(new LogoutAllRequest(Guid.NewGuid()));

        Assert.NotEqual(Guid.Empty, response.UserId);
    }

    [Fact]
    public async Task ShouldRecordTheLogoutAllEventInsideTheUnitOfWork()
    {
        var userId = Guid.NewGuid();
        var unitOfWork = new FakeUnitOfWork();
        var auditLog = new FakeAuditLog();
        var interactor = new LogoutAllInteractor(new FakeRefreshTokenRepository(), unitOfWork, auditLog);

        await interactor.ExecuteAsync(new LogoutAllRequest(userId));

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal(AuditEventType.LogoutAll, auditEvent.Type);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
        Assert.Equal(userId, auditEvent.UserExternalId);
        Assert.Equal(1, unitOfWork.Commits);
    }

    [Fact]
    public async Task ShouldRollBackWhenTheLogoutAllEventCannotBeRecorded()
    {
        var unitOfWork = new FakeUnitOfWork();
        var interactor = new LogoutAllInteractor(
            new FakeRefreshTokenRepository(),
            unitOfWork,
            new FakeAuditLog { RecordException = new InvalidOperationException("audit failed") });

        await Assert.ThrowsAsync<InvalidOperationException>(() => interactor.ExecuteAsync(new LogoutAllRequest(Guid.NewGuid())));

        Assert.Equal(0, unitOfWork.Commits);
        Assert.Equal(1, unitOfWork.Rollbacks);
    }
}
