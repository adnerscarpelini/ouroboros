using Ouroboros.AuthService.Domain;
using Ouroboros.AuthService.Infrastructure;

namespace Ouroboros.AuthService.Infrastructure.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(AuthDatabaseCollection.Name)]
public sealed class TokenTypeRepositoryTests
{
	private readonly AuthDatabaseFixture _fixture;

	public TokenTypeRepositoryTests(AuthDatabaseFixture fixture)
	{
		_fixture = fixture;
	}

	[Theory]
	[InlineData(TokenTypeNames.UserCreationValidation)]
	[InlineData(TokenTypeNames.PasswordReset)]
	public async Task GetByNameAsync_returns_the_seeded_token_type(string name)
	{
		await using var session = _fixture.CreateSession();
		var repository = new TokenTypeRepository(session);

		var tokenType = await repository.GetByNameAsync(name, CancellationToken.None);

		Assert.Equal(name, tokenType.Name);
		Assert.True(tokenType.Id > 0);
	}

	[Fact]
	public async Task GetByNameAsync_throws_when_the_name_is_not_seeded()
	{
		await using var session = _fixture.CreateSession();
		var repository = new TokenTypeRepository(session);

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			repository.GetByNameAsync($"tipo-inexistente-{Guid.NewGuid():N}", CancellationToken.None));
	}
}
