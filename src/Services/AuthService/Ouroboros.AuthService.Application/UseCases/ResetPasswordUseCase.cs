using Ouroboros.BuildingBlocks.Application;
using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Application;

public sealed class ResetPasswordUseCase : IResetPasswordUseCase
{
	private readonly IUserRepository _userRepository;
	private readonly ITokenRepository _tokenRepository;
	private readonly IUnitOfWork _unitOfWork;
	private readonly IPasswordHasher _passwordHasher;
	private readonly ITokenGenerator _tokenGenerator;

	public ResetPasswordUseCase(
		IUserRepository userRepository,
		ITokenRepository tokenRepository,
		IUnitOfWork unitOfWork,
		IPasswordHasher passwordHasher,
		ITokenGenerator tokenGenerator
	)
	{
		_userRepository = userRepository;
		_tokenRepository = tokenRepository;
		_unitOfWork = unitOfWork;
		_passwordHasher = passwordHasher;
		_tokenGenerator = tokenGenerator;
	}

	public async Task<Result> ResetPasswordAsync(
		string token,
		string newPassword,
		CancellationToken cancellationToken
	)
	{
		var storedToken = await _tokenRepository.GetByHashAsync(
			tokenHash: _tokenGenerator.Hash(token),
			cancellationToken: cancellationToken
		);

		if (storedToken is null)
		{
			return Result.Failure("Token inválido.");
		}

		if (storedToken.Validated)
		{
			return Result.Failure("Token já foi utilizado.");
		}

		if (storedToken.ExpiresAt < DateTime.UtcNow)
		{
			return Result.Failure("Token expirado.");
		}

		if (storedToken.TokenType.Name != TokenTypeNames.PasswordReset)
		{
			return Result.Failure("Token inválido.");
		}

		storedToken.Validate();
		storedToken.User.ResetPassword(_passwordHasher.Hash(newPassword));
		_tokenRepository.Update(storedToken);
		_userRepository.Update(storedToken.User);

		await _unitOfWork.SaveChangesAsync(cancellationToken);

		return Result.Success();
	}
}
