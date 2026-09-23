namespace Ouroboros.Auth.Domain.Exceptions;

public class UserNotFoundException : DomainException
{
    public UserNotFoundException() : base("User not found")
    {
    }
}
