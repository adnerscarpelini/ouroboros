namespace Ouroboros.Auth.Application.UseCases.Login;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.Settings;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Ouroboros.Auth.Domain.Policies;

public sealed class LoginInteractor : ILoginUseCase
{
    private const string BearerTokenType = "Bearer";

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly RefreshTokenSettings _refreshTokenSettings;
    private readonly IUnitOfWork _unitOfWork;

    public LoginInteractor(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        ITokenGenerator tokenGenerator,
        RefreshTokenSettings refreshTokenSettings,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _tokenGenerator = tokenGenerator;
        _refreshTokenSettings = refreshTokenSettings;
        _unitOfWork = unitOfWork;
    }

    public async Task<LoginResponse> ExecuteAsync(LoginRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var user = string.IsNullOrWhiteSpace(request.Login)
            ? null
            : await _userRepository.GetByLoginAsync(IdentityPolicy.Normalize(request.Login));
        var canVerifyRealHash = user is not null && !user.IsLockedOut(now);
        var hash = canVerifyRealHash ? user!.PasswordHash : _passwordHasher.DummyHash;
        var passwordMatches = _passwordHasher.Verify(request.Password ?? string.Empty, hash);

        if (!canVerifyRealHash || !passwordMatches)
        {
            if (canVerifyRealHash)
            {
                await _userRepository.RecordFailedAccessAsync(user!.ExternalId, now);
            }

            throw new InvalidCredentialsException();
        }

        if (!user!.Active)
        {
            throw new DomainException("User is not active");
        }

        // Varias sessoes simultaneas (spec 2026092506): o login nao revoga as anteriores, cada dispositivo tem a sua.
        var sessionId = Guid.NewGuid();
        var rawRefreshToken = _tokenGenerator.Generate();
        var refreshToken = RefreshToken.Create(
            user.ExternalId,
            _tokenGenerator.Hash(rawRefreshToken),
            now.Add(_refreshTokenSettings.Lifetime),
            sessionId);

        // Hash gerado com custo menor que o atual e refeito agora, que a senha em texto puro esta em maos. Calculado fora
        // da transacao (e lento de proposito). O UPDATE so age se o hash ainda for o lido: uma troca de senha em paralelo vence.
        var rehashedPassword = _passwordHasher.NeedsRehash(user.PasswordHash)
            ? _passwordHasher.Hash(request.Password!)
            : null;

        // Registro do login, re-hash e refresh token valem juntos ou nao valem. Os tokens so saem depois do commit.
        await _unitOfWork.ExecuteAsync(() => IssueSessionAsync(user, refreshToken, rehashedPassword, now));

        var accessToken = _jwtTokenGenerator.Generate(
            user.ExternalId,
            user.Login,
            user.Email,
            user.Role,
            sessionId);

        return new LoginResponse(
            BearerTokenType,
            accessToken.Value,
            accessToken.ExpiresAt,
            rawRefreshToken,
            refreshToken.ExpiresAt);
    }

    private async Task IssueSessionAsync(
        User user,
        RefreshToken refreshToken,
        string? rehashedPassword,
        DateTimeOffset now)
    {
        // Zera o contador de falhas e grava last_login_at. Se a conta foi bloqueada entre a leitura e aqui, recusa
        // com o mesmo erro generico.
        if (!await _userRepository.TryRegisterLoginAsync(user.ExternalId, now))
        {
            throw new InvalidCredentialsException();
        }

        if (rehashedPassword is not null)
        {
            await _userRepository.TryRehashPasswordAsync(user.ExternalId, user.PasswordHash, rehashedPassword);
        }

        await _refreshTokenRepository.AddAsync(refreshToken);
    }
}
