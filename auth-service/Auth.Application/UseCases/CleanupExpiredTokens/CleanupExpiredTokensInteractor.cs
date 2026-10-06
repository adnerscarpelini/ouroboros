namespace Ouroboros.Auth.Application.UseCases.CleanupExpiredTokens;

using Ouroboros.Auth.Application.Gateways;

public sealed class CleanupExpiredTokensInteractor : ICleanupExpiredTokensUseCase
{
    // Cada lote e um DELETE, ou seja, uma transacao curta: locks curtos mesmo com muito o que apagar.
    public const int BatchSize = 1_000;

    private readonly ITokenRepository _tokenRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public CleanupExpiredTokensInteractor(
        ITokenRepository tokenRepository,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _tokenRepository = tokenRepository;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<CleanupExpiredTokensResponse> ExecuteAsync(
        CleanupExpiredTokensRequest request,
        CancellationToken cancellationToken = default)
    {
        // Tokens ativos e revogados que ainda nao expiraram nunca entram: a deteccao de reuso (spec 2026092507) precisa
        // dos revogados ate expirarem.
        var expiredBefore = DateTimeOffset.UtcNow - request.Retention;

        var tokens = await DeleteInBatchesAsync(
            batchSize => _tokenRepository.DeleteExpiredBatchAsync(expiredBefore, batchSize),
            cancellationToken);
        var refreshTokens = await DeleteInBatchesAsync(
            batchSize => _refreshTokenRepository.DeleteExpiredBatchAsync(expiredBefore, batchSize),
            cancellationToken);

        return new CleanupExpiredTokensResponse(tokens, refreshTokens);
    }

    private static async Task<int> DeleteInBatchesAsync(
        Func<int, Task<int>> deleteBatch,
        CancellationToken cancellationToken)
    {
        var total = 0;
        int deleted;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            deleted = await deleteBatch(BatchSize);
            total += deleted;
        }
        while (deleted == BatchSize);

        return total;
    }
}
