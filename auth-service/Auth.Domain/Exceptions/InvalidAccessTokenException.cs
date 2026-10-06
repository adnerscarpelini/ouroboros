namespace Ouroboros.Auth.Domain.Exceptions;

/// <summary>
/// O access token e valido (assinatura e prazo), mas a conta de quem o apresenta nao existe mais ou foi excluida.
/// </summary>
public class InvalidAccessTokenException : DomainException
{
    public InvalidAccessTokenException() : base("Invalid access token")
    {
    }
}
