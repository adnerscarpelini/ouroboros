using Microsoft.EntityFrameworkCore;
using Ouroboros.Common.Application;
using Ouroboros.Modules.Auth.Application;
using Ouroboros.Modules.Auth.Domain;

namespace Ouroboros.Modules.Auth.Infrastructure;

public sealed class UserService : IUserService
{
	private readonly AuthDbContext _dbContext;
	private readonly IPasswordHasher _passwordHasher;

	public UserService(
		AuthDbContext dbContext,
		IPasswordHasher passwordHasher
	)
	{
		_dbContext = dbContext;
		_passwordHasher = passwordHasher;
	}

	public async Task<Result<Guid>> CreateUserAsync(
		string login,
		string fullName,
		string email,
		string password,
		CancellationToken cancellationToken
	)
	{
		var loginInUse = await _dbContext.Users.AnyAsync(u => u.Login == login, cancellationToken);

		if (loginInUse)
		{
			return Result<Guid>.Failure("Login já está em uso.");
		}

		var emailInUse = await _dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken);

		if (emailInUse)
		{
			return Result<Guid>.Failure("E-mail já está em uso.");
		}

		var passwordHash = _passwordHasher.Hash(password);

		var user = new User(
			login: login,
			fullName: fullName,
			email: email,
			passwordHash: passwordHash
		);

		_dbContext.Users.Add(user);

		await _dbContext.SaveChangesAsync(cancellationToken);

		return Result<Guid>.Success(user.ExternalId);
	}
}
