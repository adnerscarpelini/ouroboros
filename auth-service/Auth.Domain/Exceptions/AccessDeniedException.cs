namespace Ouroboros.Auth.Domain.Exceptions;

public class AccessDeniedException : DomainException
{
    public AccessDeniedException() : base("Access denied")
    {
    }
}
