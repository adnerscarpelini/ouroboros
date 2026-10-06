namespace Ouroboros.Auth.Application.UseCases.CleanupExpiredTokens;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Xunit;

// Spec 2026092515: laco de lotes, retencao e contagem por tabela.
public class CleanupExpiredTokensInteractorTests
{
    private sealed class FakeTokenRepository : ITokenRepository
    {
        public Queue<int> Batches { get; } = new();

        public List<(DateTimeOffset ExpiredBefore, int BatchSize)> Calls { get; } = new();

        public Task<int> DeleteExpiredBatchAsync(
            DateTimeOffset expiredBefore,
            int batchSize)
        {
            Calls.Add((expiredBefore, batchSize));
            return Task.FromResult(Batches.Count > 0 ? Batches.Dequeue() : 0);
        }

        public Task AddAsync(Token token) => throw new NotSupportedException();

        public Task<Token?> GetByHashAsync(
            string tokenHash,
            TokenType type) => throw new NotSupportedException();

        public Task<bool> ExistsPendingByUserAsync(
            Guid userExternalId,
            TokenType type,
            DateTimeOffset now) => throw new NotSupportedException();

        public Task UpdateAsync(Token token) => throw new NotSupportedException();

        public Task<bool> TryMarkAsUsedAsync(Token token) => throw new NotSupportedException();

        public Task InvalidatePendingByUserAsync(
            Guid userExternalId,
            TokenType type,
            DateTimeOffset invalidatedAt) => throw new NotSupportedException();
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public Queue<int> Batches { get; } = new();

        public List<(DateTimeOffset ExpiredBefore, int BatchSize)> Calls { get; } = new();

        public Task<int> DeleteExpiredBatchAsync(
            DateTimeOffset expiredBefore,
            int batchSize)
        {
            Calls.Add((expiredBefore, batchSize));
            return Task.FromResult(Batches.Count > 0 ? Batches.Dequeue() : 0);
        }

        public Task AddAsync(RefreshToken refreshToken) => throw new NotSupportedException();

        public Task<RefreshToken?> GetByHashAsync(string tokenHash) => throw new NotSupportedException();

        public Task<bool> TryRevokeAsync(RefreshToken refreshToken) => throw new NotSupportedException();

        public Task RevokeAllActiveByUserAsync(
            Guid userExternalId,
            DateTimeOffset revokedAt) => throw new NotSupportedException();

        public Task RevokeAllActiveByUserExceptSessionAsync(
            Guid userExternalId,
            Guid exceptSessionId,
            DateTimeOffset revokedAt) => throw new NotSupportedException();

        public Task RevokeAllActiveBySessionAsync(
            Guid sessionId,
            DateTimeOffset revokedAt) => throw new NotSupportedException();
    }

    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    private static CleanupExpiredTokensInteractor CreateInteractor(
        FakeTokenRepository tokens,
        FakeRefreshTokenRepository refreshTokens)
    {
        return new CleanupExpiredTokensInteractor(tokens, refreshTokens);
    }

    [Fact]
    public async Task ShouldUseBatchesOfOneThousandRows()
    {
        Assert.Equal(1_000, CleanupExpiredTokensInteractor.BatchSize);

        var tokens = new FakeTokenRepository();
        var refreshTokens = new FakeRefreshTokenRepository();

        await CreateInteractor(tokens, refreshTokens).ExecuteAsync(new CleanupExpiredTokensRequest(Retention));

        Assert.All(tokens.Calls, call => Assert.Equal(1_000, call.BatchSize));
        Assert.All(refreshTokens.Calls, call => Assert.Equal(1_000, call.BatchSize));
    }

    [Fact]
    public async Task ShouldDeleteOnlyRowsExpiredBeforeNowMinusRetention()
    {
        var tokens = new FakeTokenRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var before = DateTimeOffset.UtcNow;

        await CreateInteractor(tokens, refreshTokens).ExecuteAsync(new CleanupExpiredTokensRequest(Retention));

        var after = DateTimeOffset.UtcNow;
        var cutoff = Assert.Single(tokens.Calls).ExpiredBefore;
        Assert.InRange(cutoff, before - Retention, after - Retention);
        Assert.Equal(cutoff, Assert.Single(refreshTokens.Calls).ExpiredBefore);
    }

    [Fact]
    public async Task ShouldKeepDeletingUntilABatchComesBackShort()
    {
        var tokens = new FakeTokenRepository();
        tokens.Batches.Enqueue(1_000);
        tokens.Batches.Enqueue(1_000);
        tokens.Batches.Enqueue(300);
        var refreshTokens = new FakeRefreshTokenRepository();
        refreshTokens.Batches.Enqueue(40);

        var response = await CreateInteractor(tokens, refreshTokens).ExecuteAsync(new CleanupExpiredTokensRequest(Retention));

        Assert.Equal(3, tokens.Calls.Count);
        Assert.Equal(2_300, response.Tokens);
        Assert.Single(refreshTokens.Calls);
        Assert.Equal(40, response.RefreshTokens);
    }

    [Fact]
    public async Task ShouldAskOnceMoreWhenTheLastBatchWasExactlyFull()
    {
        var tokens = new FakeTokenRepository();
        tokens.Batches.Enqueue(1_000);
        var refreshTokens = new FakeRefreshTokenRepository();

        var response = await CreateInteractor(tokens, refreshTokens).ExecuteAsync(new CleanupExpiredTokensRequest(Retention));

        Assert.Equal(2, tokens.Calls.Count);
        Assert.Equal(1_000, response.Tokens);
    }

    [Fact]
    public async Task ShouldCountEachTableSeparately()
    {
        var tokens = new FakeTokenRepository();
        tokens.Batches.Enqueue(7);
        var refreshTokens = new FakeRefreshTokenRepository();
        refreshTokens.Batches.Enqueue(11);

        var response = await CreateInteractor(tokens, refreshTokens).ExecuteAsync(new CleanupExpiredTokensRequest(Retention));

        Assert.Equal(new CleanupExpiredTokensResponse(7, 11), response);
    }

    [Fact]
    public async Task ShouldStopBetweenBatchesWhenCancelled()
    {
        var tokens = new FakeTokenRepository();
        tokens.Batches.Enqueue(1_000);
        var refreshTokens = new FakeRefreshTokenRepository();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateInteractor(tokens, refreshTokens).ExecuteAsync(new CleanupExpiredTokensRequest(Retention), cancellation.Token));

        Assert.Empty(tokens.Calls);
    }
}
