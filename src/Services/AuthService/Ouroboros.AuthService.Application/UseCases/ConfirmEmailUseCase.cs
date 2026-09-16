using Ouroboros.BuildingBlocks.Application;
using Ouroboros.AuthService.Domain;

namespace Ouroboros.AuthService.Application;

public sealed class ConfirmEmailUseCase : IConfirmEmailUseCase
{
	private readonly IUserRepository _userRepository;
	private readonly ITokenRepository _tokenRepository;
	private readonly IUnitOfWork _unitOfWork;
	private readonly ITokenGenerator _tokenGenerator;

	public ConfirmEmailUseCase(
		IUserRepository userRepository,
		ITokenRepository tokenRepository,
		IUnitOfWork unitOfWork,
		ITokenGenerator tokenGenerator
	)
	{
		_userRepository = userRepository;
		_tokenRepository = tokenRepository;
		_unitOfWork = unitOfWork;
		_tokenGenerator = tokenGenerator;
	}

	public async Task<Result> ConfirmEmailAsync(
		string token,
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

		if (storedToken.TokenType.Name != TokenTypeNames.UserCreationValidation)
		{
			return Result.Failure("Token inválido.");
		}

		storedToken.Validate();
		storedToken.User.ConfirmEmail();
		_tokenRepository.Update(storedToken);
		_userRepository.Update(storedToken.User);

		await _unitOfWork.SaveChangesAsync(cancellationToken);

		return Result.Success();
	}
}
