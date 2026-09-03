using Ouroboros.Services.Auth.Domain;

namespace Ouroboros.Services.Auth.Application.Tests;

public sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
	private readonly List<RefreshToken> _refreshTokens = new();

	public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens;

	public void Add(RefreshToken refreshToken)
	{
		_refreshTokens.Add(refreshToken);
	}

	public Task<RefreshToken?> GetByHashAsync(
		string tokenHash,
		CancellationToken cancellationToken
	)
	{
		return Task.FromResult(_refreshTokens.SingleOrDefault(t => t.TokenHash == tokenHash));
	}
}
