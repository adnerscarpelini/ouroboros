using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.AuthService.Domain;
using Ouroboros.AuthService.Infrastructure;

namespace Ouroboros.AuthService.Infrastructure.Tests.Integration;

internal static class AuthTestData
{
	public static User NewUser()
	{
		var unique = Guid.NewGuid().ToString("N");

		return new User(
			login: $"user_{unique}",
			fullName: "Usuário de Teste",
			email: $"{unique}@example.com",
			passwordHash: "hash-de-teste"
		);
	}

	public static async Task<User> CreateAndPersistUserAsync(
		DbSession session,
		CancellationToken cancellationToken)
	{
		var user = NewUser();
		var repository = new UserRepository(session);
		var unitOfWork = new UnitOfWork(session);

		repository.Add(user);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return user;
	}
}
