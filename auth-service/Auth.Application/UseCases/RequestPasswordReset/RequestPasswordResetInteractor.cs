namespace Ouroboros.Auth.Application.UseCases.RequestPasswordReset;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Ouroboros.Auth.Domain.Policies;

public sealed class RequestPasswordResetInteractor : IRequestPasswordResetUseCase
{
    private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    private readonly IUserRepository _userRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLog _auditLog;

    public RequestPasswordResetInteractor(
        IUserRepository userRepository,
        ITokenRepository tokenRepository,
        ITokenGenerator tokenGenerator,
        IUnitOfWork unitOfWork,
        IAuditLog auditLog)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _tokenGenerator = tokenGenerator;
        _unitOfWork = unitOfWork;
        _auditLog = auditLog;
    }

    public async Task<RequestPasswordResetResponse> ExecuteAsync(RequestPasswordResetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LoginOrEmail))
        {
            throw new DomainException("Login or email is required");
        }

        var user = await _userRepository.GetByLoginOrEmailAsync(IdentityPolicy.Normalize(request.LoginOrEmail));

        // Usuario inexistente, inativo ou sem e-mail confirmado termina sem erro: o chamador nao pode
        // distinguir esses casos (evita enumeracao) e o reset nao vira atalho pra ativar conta.
        if (user is null || !user.Active || !user.EmailConfirmed)
        {
            return new RequestPasswordResetResponse(null, null);
        }

        var now = DateTimeOffset.UtcNow;
        var passwordResetToken = _tokenGenerator.Generate();
        var token = Token.Create(
            user.ExternalId,
            TokenType.PasswordReset,
            _tokenGenerator.Hash(passwordResetToken),
            now.Add(PasswordResetTokenLifetime));

        // Invalidar os links anteriores, gravar o novo e registrar o evento valem juntos ou nao valem. O evento so existe
        // quando a conta existe, e isso nunca e exposto ao cliente (a resposta e a mesma nos dois casos).
        await _unitOfWork.ExecuteAsync(async () =>
        {
            await _tokenRepository.InvalidatePendingByUserAsync(user.ExternalId, TokenType.PasswordReset, now);
            await _tokenRepository.AddAsync(token);
            await _auditLog.RecordAsync(new AuditEvent(AuditEventType.PasswordResetRequested, AuditOutcome.Success, user.ExternalId));
        });

        return new RequestPasswordResetResponse(user.ExternalId, passwordResetToken);
    }
}
