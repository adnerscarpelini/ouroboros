using Ouroboros.Services.Auth.Domain;
using Ouroboros.Services.Auth.Infrastructure;

namespace Ouroboros.Services.Auth.Infrastructure.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(AuthDatabaseCollection.Name)]
public sealed class TokenRepositoryTests
{
	private readonly AuthDatabaseFixture _fixture;

	public TokenRepositoryTests(AuthDatabaseFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Add_persists_token_and_makes_it_retrievable_by_hash_with_navigations_loaded()
	{
		await using var session = _fixture.CreateSession();
		var user = await AuthTestData.CreateAndPersistUserAsync(session, CancellationToken.None);
		var tokenType = await new TokenTypeRepository(session).GetByNameAsync(
			TokenTypeNames.UserCreationValidation,
			CancellationToken.None);
		var repository = new TokenRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var tokenHash = $"hash_{Guid.NewGuid():N}";
		var notificationRequestId = Guid.NewGuid();
		var token = new Token(
			tokenType: tokenType,
			user: user,
			notificationRequestId: notificationRequestId,
			tokenHash: tokenHash,
			expiresAt: DateTime.UtcNow.AddHours(1));

		repository.Add(token);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var reloaded = await repository.GetByHashAsync(tokenHash, CancellationToken.None);

		Assert.NotNull(reloaded);
		Assert.Equal(token.ExternalId, reloaded!.ExternalId);
		Assert.Equal(notificationRequestId, reloaded.NotificationRequestId);
		Assert.False(reloaded.Validated);
		Assert.Null(reloaded.ValidatedAt);
		Assert.Equal(user.ExternalId, reloaded.User.ExternalId);
		Assert.Equal(tokenType.ExternalId, reloaded.TokenType.ExternalId);
	}

	[Fact]
	public async Task GetByHashAsync_returns_null_when_the_hash_does_not_exist()
	{
		await using var session = _fixture.CreateSession();
		var repository = new TokenRepository(session);

		var reloaded = await repository.GetByHashAsync(
			$"hash-inexistente-{Guid.NewGuid():N}",
			CancellationToken.None);

		Assert.Null(reloaded);
	}

	[Fact]
	public async Task Update_persists_validation_and_stamps_updated_at()
	{
		await using var session = _fixture.CreateSession();
		var user = await AuthTestData.CreateAndPersistUserAsync(session, CancellationToken.None);
		var tokenType = await new TokenTypeRepository(session).GetByNameAsync(
			TokenTypeNames.UserCreationValidation,
			CancellationToken.None);
		var repository = new TokenRepository(session);
		var unitOfWork = new UnitOfWork(session);
		var tokenHash = $"hash_{Guid.NewGuid():N}";
		var token = new Token(tokenType, user, Guid.NewGuid(), tokenHash, DateTime.UtcNow.AddHours(1));
		repository.Add(token);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		token.Validate();
		repository.Update(token);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var reloaded = await repository.GetByHashAsync(tokenHash, CancellationToken.None);

		Assert.NotNull(reloaded);
		Assert.True(reloaded!.Validated);
		Assert.NotNull(reloaded.ValidatedAt);
		Assert.NotNull(reloaded.UpdatedAt);
	}

	[Fact]
	public async Task GetPendingByUserAndTypeAsync_only_returns_unvalidated_tokens_of_the_requested_type_for_that_user()
	{
		await using var session = _fixture.CreateSession();
		var user = await AuthTestData.CreateAndPersistUserAsync(session, CancellationToken.None);
		var otherUser = await AuthTestData.CreateAndPersistUserAsync(session, CancellationToken.None);
		var typeRepository = new TokenTypeRepository(session);
		var userCreationType = await typeRepository.GetByNameAsync(TokenTypeNames.UserCreationValidation, CancellationToken.None);
		var passwordResetType = await typeRepository.GetByNameAsync(TokenTypeNames.PasswordReset, CancellationToken.None);
		var repository = new TokenRepository(session);
		var unitOfWork = new UnitOfWork(session);

		var pendingToken = new Token(userCreationType, user, Guid.NewGuid(), $"hash_{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(1));
		var validatedToken = new Token(userCreationType, user, Guid.NewGuid(), $"hash_{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(1));
		var otherTypeToken = new Token(passwordResetType, user, Guid.NewGuid(), $"hash_{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(1));
		var otherUserToken = new Token(userCreationType, otherUser, Guid.NewGuid(), $"hash_{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(1));
		repository.Add(pendingToken);
		repository.Add(validatedToken);
		repository.Add(otherTypeToken);
		repository.Add(otherUserToken);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		validatedToken.Validate();
		repository.Update(validatedToken);
		await unitOfWork.SaveChangesAsync(CancellationToken.None);

		var pending = await repository.GetPendingByUserAndTypeAsync(
			user,
			TokenTypeNames.UserCreationValidation,
			CancellationToken.None);

		var pendingExternalIds = pending.Select(token => token.ExternalId).ToArray();
		Assert.Contains(pendingToken.ExternalId, pendingExternalIds);
		Assert.DoesNotContain(validatedToken.ExternalId, pendingExternalIds);
		Assert.DoesNotContain(otherTypeToken.ExternalId, pendingExternalIds);
		Assert.DoesNotContain(otherUserToken.ExternalId, pendingExternalIds);
	}
}
