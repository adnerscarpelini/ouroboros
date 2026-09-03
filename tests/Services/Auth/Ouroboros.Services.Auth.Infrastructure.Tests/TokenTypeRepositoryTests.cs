using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Infrastructure.Tests;

public class TokenTypeRepositoryTests
{
	[Theory]
	[InlineData(TokenTypeNames.UserCreationValidation)]
	[InlineData(TokenTypeNames.PasswordReset)]
	public async Task GetByNameAsync_ReturnsTypeSeededByMigration(string tokenTypeName)
	{
		var database = new InMemoryAuthDatabase();

		await using var dbContext = database.CreateContext();

		var tokenType = await new TokenTypeRepository(dbContext).GetByNameAsync(
			name: tokenTypeName,
			cancellationToken: CancellationToken.None
		);

		Assert.Equal(tokenTypeName, tokenType.Name);
		Assert.NotEqual(0, tokenType.Id);
	}
}
