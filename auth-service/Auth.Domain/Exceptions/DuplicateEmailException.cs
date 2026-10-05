namespace Ouroboros.Auth.Domain.Exceptions;

public class DuplicateEmailException : DomainException
{
    public DuplicateEmailException() : base("Email already in use")
    {
    }
}
