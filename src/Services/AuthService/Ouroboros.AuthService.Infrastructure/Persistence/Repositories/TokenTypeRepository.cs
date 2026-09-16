using System.Data.Common;
using Npgsql;
using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.AuthService.Application;
using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Infrastructure;

public sealed class TokenTypeRepository : ITokenTypeRepository
{
	private readonly DbSession _session;

	public TokenTypeRepository(DbSession session)
	{
		_session = session;
	}

	public async Task<TokenType> GetByNameAsync(string name, CancellationToken cancellationToken)
	{
		await _session.OpenAsync(cancellationToken);
		await using var command = new NpgsqlCommand(
			"""
			SELECT
				id,
				external_id,
				created_at,
				updated_at,
				name
			FROM
				auth.token_types
			WHERE
				name = @name;
			""",
			(NpgsqlConnection)_session.Connection,
			(NpgsqlTransaction?)_session.Transaction);
		command.Parameters.AddWithValue("name", name);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
		{
			throw new InvalidOperationException($"Tipo de token '{name}' não encontrado.");
		}

		return TokenType.Rehydrate(
			id: reader.GetInt64(reader.GetOrdinal("id")),
			externalId: reader.GetGuid(reader.GetOrdinal("external_id")),
			createdAt: reader.GetDateTime(reader.GetOrdinal("created_at")),
			updatedAt: Nullable(reader, "updated_at"),
			name: reader.GetString(reader.GetOrdinal("name"))
		);
	}

	private static DateTime? Nullable(DbDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
	}
}
