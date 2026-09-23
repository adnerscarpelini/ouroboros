namespace Ouroboros.Auth.Application.UseCases.Logout;

/// <param name="Revoked"><c>false</c> quando o token ja estava revogado ou expirado (logout idempotente).</param>
public record LogoutResponse(bool Revoked);
