namespace Ouroboros.Auth.Domain.Exceptions;

public class DuplicateLoginException : DomainException
{
    public DuplicateLoginException() : base("Login already in use")
    {
    }
}
