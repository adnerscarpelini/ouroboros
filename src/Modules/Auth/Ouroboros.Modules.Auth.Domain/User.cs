namespace Ouroboros.Modules.Auth.Domain;

public sealed class User
{
	public Guid Id { get; private set; }
	public string Email { get; private set; } = null!;

	// Construtor sem parâmetros exclusivo para o EF Core materializar a entidade a partir do banco.
	private User()
	{
	}

	public User(string email)
	{
		Id = Guid.NewGuid();
		Email = email;
	}
}
