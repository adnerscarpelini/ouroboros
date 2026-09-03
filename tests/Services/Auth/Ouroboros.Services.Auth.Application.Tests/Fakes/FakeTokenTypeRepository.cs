using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application.Tests;

public sealed class FakeTokenTypeRepository : ITokenTypeRepository
{
	// Mesma instância a cada chamada, como no banco (os tipos vêm do seed da migration) — é o que
	// permite comparar TokenType por referência nos fakes de repositório.
	private readonly Dictionary<string, TokenType> _tokenTypes = new()
	{
		[TokenTypeNames.UserCreationValidation] = new TokenType(TokenTypeNames.UserCreationValidation),
		[TokenTypeNames.PasswordReset] = new TokenType(TokenTypeNames.PasswordReset)
	};

	public Task<TokenType> GetByNameAsync(
		string name,
		CancellationToken cancellationToken
	)
	{
		return Task.FromResult(_tokenTypes[name]);
	}
}
