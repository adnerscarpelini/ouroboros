namespace Ouroboros.Auth.Domain.Exceptions;

/// <summary>
/// Um refresh token ja revogado foi apresentado de novo (spec 2026092507): alguem usa uma copia dele. A resposta ao
/// cliente e o mesmo <c>Invalid refresh token</c> dos demais casos; os identificadores servem so ao log.
/// </summary>
public sealed class RefreshTokenReuseException : InvalidRefreshTokenException
{
    public RefreshTokenReuseException(
        Guid userExternalId,
        Guid sessionId)
        : base("Invalid refresh token")
    {
        UserExternalId = userExternalId;
        SessionId = sessionId;
    }

    public Guid UserExternalId { get; }

    public Guid SessionId { get; }
}
