using System.Data.Common;
using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Infrastructure;

internal static class UserRowMapper
{
	public static User Map(DbDataReader reader)
		=> Map(reader, string.Empty);

	public static User Map(DbDataReader reader, string prefix)
	{
		return User.Rehydrate(
			id: reader.GetInt64(reader.GetOrdinal(prefix + "id")),
			externalId: reader.GetGuid(reader.GetOrdinal(prefix + "external_id")),
			createdAt: reader.GetDateTime(reader.GetOrdinal(prefix + "created_at")),
			updatedAt: GetNullableDateTime(reader, prefix + "updated_at"),
			login: reader.GetString(reader.GetOrdinal(prefix + "login")),
			fullName: reader.GetString(reader.GetOrdinal(prefix + "full_name")),
			email: reader.GetString(reader.GetOrdinal(prefix + "email")),
			emailConfirmed: reader.GetBoolean(reader.GetOrdinal(prefix + "email_confirmed")),
			passwordHash: reader.GetString(reader.GetOrdinal(prefix + "password_hash")),
			passwordChangedAt: reader.GetDateTime(reader.GetOrdinal(prefix + "password_changed_at")),
			isActive: reader.GetBoolean(reader.GetOrdinal(prefix + "is_active")),
			failedLoginAttempts: reader.GetInt32(reader.GetOrdinal(prefix + "failed_login_attempts")),
			lockedUntil: GetNullableDateTime(reader, prefix + "locked_until"),
			lastLoginAt: GetNullableDateTime(reader, prefix + "last_login_at"));
	}

	private static DateTime? GetNullableDateTime(DbDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
	}
}
