namespace Ouroboros.Modules.Auth.Application;

public interface IUserService
{
	bool CreateUser(
		string email,
		string password
	);
}
