namespace Ouroboros.Auth.Application.UseCases.LogoutAll;

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
        var interactor = new LogoutAllInteractor(repository);

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
        var interactor = new LogoutAllInteractor(repository);

        await interactor.ExecuteAsync(new LogoutAllRequest(Guid.NewGuid()));

        Assert.Null(other.RevokedAt);
    }

    [Fact]
    public async Task ShouldCompleteWhenUserHasNoActiveSession()
    {
        var interactor = new LogoutAllInteractor(new FakeRefreshTokenRepository());

        var response = await interactor.ExecuteAsync(new LogoutAllRequest(Guid.NewGuid()));

        Assert.NotEqual(Guid.Empty, response.UserId);
    }
}
