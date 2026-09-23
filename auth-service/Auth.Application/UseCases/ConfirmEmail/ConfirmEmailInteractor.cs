namespace Ouroboros.Auth.Application.UseCases.ConfirmEmail;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;

public sealed class ConfirmEmailInteractor : IConfirmEmailUseCase
{
    private readonly ITokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITokenGenerator _tokenGenerator;

    public ConfirmEmailInteractor(
        ITokenRepository tokenRepository,
        IUserRepository userRepository,
        ITokenGenerator tokenGenerator)
    {
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<ConfirmEmailResponse> ExecuteAsync(ConfirmEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new DomainException("Confirmation token is required");
        }

        var tokenHash = _tokenGenerator.Hash(request.Token);
        var token = await _tokenRepository.GetByHashAsync(tokenHash, TokenType.EmailConfirmation);

        if (token is null)
        {
            throw new DomainException("Invalid confirmation token");
        }

        var user = await _userRepository.GetByExternalIdAsync(token.UserExternalId);

        if (user is null)
        {
            throw new DomainException("Invalid confirmation token");
        }

        token.MarkAsUsed(DateTimeOffset.UtcNow);
        user.ConfirmEmail();

        await _userRepository.UpdateAsync(user);
        await _tokenRepository.UpdateAsync(token);

        return new ConfirmEmailResponse(user.ExternalId, user.Login, user.Email);
    }
}
