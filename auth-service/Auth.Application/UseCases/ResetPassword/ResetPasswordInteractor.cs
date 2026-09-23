namespace Ouroboros.Auth.Application.UseCases.ResetPassword;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Ouroboros.Auth.Domain.Policies;

public sealed class ResetPasswordInteractor : IResetPasswordUseCase
{
    // Mesma mensagem pra token vazio, inexistente, de outro tipo, expirado ou ja usado: nao revela qual caso ocorreu.
    private const string InvalidTokenMessage = "Invalid or expired password reset token";

    private readonly ITokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;

    public ResetPasswordInteractor(
        ITokenRepository tokenRepository,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator)
    {
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<ResetPasswordResponse> ExecuteAsync(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new DomainException(InvalidTokenMessage);
        }

        var now = DateTimeOffset.UtcNow;
        var token = await _tokenRepository.GetByHashAsync(_tokenGenerator.Hash(request.Token), TokenType.PasswordReset);

        if (token is null || !token.IsPending(now))
        {
            throw new DomainException(InvalidTokenMessage);
        }

        var user = await _userRepository.GetByExternalIdAsync(token.UserExternalId);

        if (user is null)
        {
            throw new DomainException(InvalidTokenMessage);
        }

        if (!user.Active)
        {
            throw new DomainException("User is not active");
        }

        // Senha rejeitada nao consome o token: o usuario pode tentar de novo com o mesmo link.
        PasswordPolicy.Validate(request.NewPassword);

        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            throw new DomainException("New password must be different from the current password");
        }

        token.MarkAsUsed(now);

        var marked = await _tokenRepository.TryMarkAsUsedAsync(token);

        if (!marked)
        {
            throw new DomainException(InvalidTokenMessage);
        }

        user.ChangePassword(_passwordHasher.Hash(request.NewPassword));

        await _userRepository.UpdateAsync(user);

        // Encerra todas as sessoes, inclusive as de quem eventualmente tinha a senha antiga.
        await _refreshTokenRepository.RevokeAllActiveByUserAsync(user.ExternalId, now);

        return new ResetPasswordResponse(user.ExternalId);
    }
}
