namespace Ouroboros.Auth.Application.UseCases.CleanupExpiredTokens;

public interface ICleanupExpiredTokensUseCase
{
    Task<CleanupExpiredTokensResponse> ExecuteAsync(
        CleanupExpiredTokensRequest request,
        CancellationToken cancellationToken = default);
}
