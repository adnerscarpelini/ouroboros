namespace Ouroboros.Auth.Domain.Exceptions;

public class InvalidRefreshTokenException : DomainException
{
    public InvalidRefreshTokenException(string message) : base(message)
    {
    }
}
