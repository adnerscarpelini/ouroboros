using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application.Tests;

public sealed class FakeTokenRepository : ITokenRepository
{
	private readonly List<Token> _tokens = new();

	public IReadOnlyCollection<Token> Tokens => _tokens;

	public void Add(Token token)
	{
		_tokens.Add(token);
	}

	public Task<Token?> GetByHashAsync(
		string tokenHash,
		CancellationToken cancellationToken
	)
	{
		return Task.FromResult(_tokens.SingleOrDefault(t => t.TokenHash == tokenHash));
	}

	public Task<IReadOnlyCollection<Token>> GetPendingByUserAndTypeAsync(
		User user,
		string tokenTypeName,
		CancellationToken cancellationToken
	)
	{
		IReadOnlyCollection<Token> pendingTokens = _tokens
			.Where(t => t.User == user && t.TokenType.Name == tokenTypeName && !t.Validated)
			.ToList();

		return Task.FromResult(pendingTokens);
	}
}
