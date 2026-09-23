namespace Ouroboros.Auth.Application.UseCases.Login;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.Settings;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;

public sealed class LoginInteractor : ILoginUseCase
{
    private const string BearerTokenType = "Bearer";

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly RefreshTokenSettings _refreshTokenSettings;

    public LoginInteractor(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ITokenGenerator tokenGenerator,
        RefreshTokenSettings refreshTokenSettings)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _tokenGenerator = tokenGenerator;
        _refreshTokenSettings = refreshTokenSettings;
    }

    public async Task<LoginResponse> ExecuteAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrEmpty(request.Password))
        {
            throw new InvalidCredentialsException();
        }

        var user = await _userRepository.GetByLoginAsync(request.Login.Trim());

        // Login inexistente e senha errada geram o mesmo erro pra nao revelar quais logins existem.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        if (!user.Active)
        {
            throw new DomainException("User is not active");
        }

        // Reautenticacao encerra as sessoes anteriores: so o par emitido agora continua valido.
        // Estou fazendo assim porque atualmente eu não criei uma rotina automatica de revogacao de refresh tokens,
        await _refreshTokenRepository.RevokeAllActiveByUserAsync(user.ExternalId, DateTimeOffset.UtcNow);

        var accessToken = _jwtTokenGenerator.Generate(
            user.ExternalId,
            user.Login,
            user.Email,
            user.Role);

        var rawRefreshToken = _tokenGenerator.Generate();
        var refreshToken = RefreshToken.Create(
            user.ExternalId,
            _tokenGenerator.Hash(rawRefreshToken),
            DateTimeOffset.UtcNow.Add(_refreshTokenSettings.Lifetime));

        await _refreshTokenRepository.AddAsync(refreshToken);

        return new LoginResponse(
            BearerTokenType,
            accessToken.Value,
            accessToken.ExpiresAt,
            rawRefreshToken,
            refreshToken.ExpiresAt);
    }
}
