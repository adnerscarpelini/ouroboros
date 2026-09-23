namespace Ouroboros.Auth.Domain.Exceptions;

public class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException() : base("Invalid login or password")
    {
    }
}
