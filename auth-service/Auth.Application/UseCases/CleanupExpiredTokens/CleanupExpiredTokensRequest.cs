namespace Ouroboros.Auth.Application.UseCases.CleanupExpiredTokens;

/// <param name="Retention">
/// Quanto tempo uma linha fica depois de expirar. So saem as com <c>expires_at &lt; agora - Retention</c>.
/// </param>
public record CleanupExpiredTokensRequest(TimeSpan Retention);
