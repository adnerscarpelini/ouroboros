namespace Ouroboros.Auth.Application.UseCases.RefreshAccessToken;

public interface IRefreshAccessTokenUseCase
{
    Task<RefreshAccessTokenResponse> ExecuteAsync(RefreshAccessTokenRequest request);
}
