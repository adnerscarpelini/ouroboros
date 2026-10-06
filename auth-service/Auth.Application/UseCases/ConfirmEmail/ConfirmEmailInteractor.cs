namespace Ouroboros.Auth.Application.UseCases.ConfirmEmail;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;

public sealed class ConfirmEmailInteractor : IConfirmEmailUseCase
{
    // Mesma mensagem pra token vazio, inexistente, de outro tipo, expirado, ja usado ou de conta inexistente ou excluida:
    // nao revela qual caso ocorreu.
    private const string InvalidTokenMessage = "Invalid or expired confirmation token";

    private readonly ITokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmEmailInteractor(
        ITokenRepository tokenRepository,
        IUserRepository userRepository,
        ITokenGenerator tokenGenerator,
        IUnitOfWork unitOfWork)
    {
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _tokenGenerator = tokenGenerator;
        _unitOfWork = unitOfWork;
    }

    public async Task<ConfirmEmailResponse> ExecuteAsync(ConfirmEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new DomainException(InvalidTokenMessage);
        }

        var now = DateTimeOffset.UtcNow;
        var token = await _tokenRepository.GetByHashAsync(_tokenGenerator.Hash(request.Token), TokenType.EmailConfirmation);

        // Descarta o obvio sem abrir transacao. A garantia de uso unico vem do UPDATE condicional abaixo.
        if (token is null || !token.IsPending(now))
        {
            throw new DomainException(InvalidTokenMessage);
        }

        // Consumo do token e ativacao do usuario valem juntos ou nao valem.
        return await _unitOfWork.ExecuteAsync(() => ConfirmAsync(token, now));
    }

    private async Task<ConfirmEmailResponse> ConfirmAsync(
        Token token,
        DateTimeOffset now)
    {
        token.MarkAsUsed(now);

        // Duas confirmacoes simultaneas chegam aqui juntas: so uma afeta a linha.
        if (!await _tokenRepository.TryMarkAsUsedAsync(token))
        {
            throw new DomainException(InvalidTokenMessage);
        }

        // O usuario e lido depois de consumir o token, ja dentro da transacao, pra respeitar uma exclusao
        // que tenha sido confirmada nesse meio tempo (o UpdateAsync regrava a linha toda com o que foi lido).
        var user = await _userRepository.GetByExternalIdAsync(token.UserExternalId);

        if (user is null)
        {
            throw new DomainException(InvalidTokenMessage);
        }

        user.ConfirmEmail();

        await _userRepository.UpdateAsync(user);

        return new ConfirmEmailResponse(user.ExternalId, user.Login, user.Email);
    }
}
