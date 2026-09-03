using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application.Tests;

public sealed class FakeUserRepository : IUserRepository
{
	private readonly List<User> _users = new();

	public IReadOnlyCollection<User> Users => _users;

	public void Add(User user)
	{
		_users.Add(user);
	}

	public Task<User?> GetByLoginAsync(
		string login,
		CancellationToken cancellationToken
	)
	{
		return Task.FromResult(_users.SingleOrDefault(u => u.Login == login));
	}

	public Task<User?> GetByEmailAsync(
		string email,
		CancellationToken cancellationToken
	)
	{
		return Task.FromResult(_users.SingleOrDefault(u => u.Email == email));
	}

	public Task<bool> ExistsByLoginAsync(
		string login,
		CancellationToken cancellationToken
	)
	{
		return Task.FromResult(_users.Any(u => u.Login == login));
	}

	public Task<bool> ExistsByEmailAsync(
		string email,
		CancellationToken cancellationToken
	)
	{
		return Task.FromResult(_users.Any(u => u.Email == email));
	}
}
