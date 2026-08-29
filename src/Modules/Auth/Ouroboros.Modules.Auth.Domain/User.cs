namespace Ouroboros.Modules.Auth.Domain;

public sealed class User
{
	public Guid Id { get; }
	public string Email { get; }

	public User(string email)
	{
		Id = Guid.NewGuid();
		Email = email;
	}
}
