using Ouroboros.Common.Domain;

namespace Ouroboros.Modules.Auth.Domain;

public sealed class User : Entity
{
	public string Login { get; private set; } = null!;
	public string FullName { get; private set; } = null!;
	public string Email { get; private set; } = null!;
	public bool EmailConfirmed { get; private set; }
	public string PasswordHash { get; private set; } = null!;
	public DateTime PasswordChangedAt { get; private set; }
	public bool IsActive { get; private set; }
	public int FailedLoginAttempts { get; private set; }
	public DateTime? LockedUntil { get; private set; }
	public DateTime? LastLoginAt { get; private set; }

	// Construtor sem parâmetros exclusivo para o EF Core materializar a entidade a partir do banco.
	private User()
	{
	}

	public User(
		string login,
		string fullName,
		string email,
		string passwordHash
	)
	{
		Login = login;
		FullName = fullName;
		Email = email;
		EmailConfirmed = false;
		PasswordHash = passwordHash;
		PasswordChangedAt = CreatedAt;
		// Ativado só depois da confirmação de e-mail (fluxo ainda não implementado).
		IsActive = false;
		FailedLoginAttempts = 0;
		LockedUntil = null;
		LastLoginAt = null;
	}
}
