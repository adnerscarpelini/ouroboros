namespace Ouroboros.Auth.Application.UseCases.RefreshAccessToken;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.Settings;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;

public sealed class RefreshAccessTokenInteractor : IRefreshAccessTokenUseCase
{
    private const string BearerTokenType = "Bearer";

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly RefreshTokenSettings _refreshTokenSettings;

    public RefreshAccessTokenInteractor(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        ITokenGenerator tokenGenerator,
        RefreshTokenSettings refreshTokenSettings)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _tokenGenerator = tokenGenerator;
        _refreshTokenSettings = refreshTokenSettings;
    }

    public async Task<RefreshAccessTokenResponse> ExecuteAsync(RefreshAccessTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new InvalidRefreshTokenException("Refresh token is required");
        }

        var currentToken = await _refreshTokenRepository.GetByHashAsync(_tokenGenerator.Hash(request.RefreshToken));

        if (currentToken is null)
        {
            throw new InvalidRefreshTokenException("Invalid refresh token");
        }

        var user = await _userRepository.GetByExternalIdAsync(currentToken.UserExternalId);

        if (user is null)
        {
            throw new InvalidRefreshTokenException("Invalid refresh token");
        }

        if (!user.Active)
        {
            throw new DomainException("User is not active");
        }

        currentToken.Revoke(DateTimeOffset.UtcNow);

        // Rotacao: o token atual so vale uma vez. Se outra requisicao revogou antes, rejeita.
        var revoked = await _refreshTokenRepository.TryRevokeAsync(currentToken);

        if (!revoked)
        {
            throw new InvalidRefreshTokenException("Refresh token has been revoked");
        }

        var accessToken = _jwtTokenGenerator.Generate(
            user.ExternalId,
            user.Login,
            user.Email,
            user.Role);

        var rawRefreshToken = _tokenGenerator.Generate();
        var newToken = RefreshToken.Create(
            user.ExternalId,
            _tokenGenerator.Hash(rawRefreshToken),
            DateTimeOffset.UtcNow.Add(_refreshTokenSettings.Lifetime));

        await _refreshTokenRepository.AddAsync(newToken);

        return new RefreshAccessTokenResponse(
            BearerTokenType,
            accessToken.Value,
            accessToken.ExpiresAt,
            rawRefreshToken,
            newToken.ExpiresAt);
    }
}
