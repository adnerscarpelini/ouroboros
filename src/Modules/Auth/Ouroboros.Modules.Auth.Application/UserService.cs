namespace Ouroboros.Modules.Auth.Application;

public sealed class UserService : IUserService
{
	// Stub proposital: sem validação nem persistência ainda. Vira implementação real
	// quando o módulo Auth ganhar uma camada de Infrastructure.
	public bool CreateUser(
		string email,
		string password
	)
	{
		return true;
	}
}
