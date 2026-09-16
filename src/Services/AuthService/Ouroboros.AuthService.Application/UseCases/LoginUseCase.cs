using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.AuthService.Application;

public sealed class LoginUseCase : ILoginUseCase
{
	private readonly IUserRepository _userRepository;
	private readonly IUnitOfWork _unitOfWork;
	private readonly IPasswordHasher _passwordHasher;
	private readonly AuthenticationResultFactory _authenticationResultFactory;

	public LoginUseCase(
		IUserRepository userRepository,
		IUnitOfWork unitOfWork,
		IPasswordHasher passwordHasher,
		AuthenticationResultFactory authenticationResultFactory
	)
	{
		_userRepository = userRepository;
		_unitOfWork = unitOfWork;
		_passwordHasher = passwordHasher;
		_authenticationResultFactory = authenticationResultFactory;
	}

	public async Task<Result<AuthenticationResult>> LoginAsync(
		string login,
		string password,
		CancellationToken cancellationToken
	)
	{
		var user = await _userRepository.GetByLoginAsync(
			login: login,
			cancellationToken: cancellationToken
		);

		if (user is null)
		{
			return Result<AuthenticationResult>.Failure("Login ou senha inválidos.");
		}

		if (user.IsLockedOut())
		{
			return Result<AuthenticationResult>.Failure("Conta temporariamente bloqueada por excesso de tentativas. Tente novamente mais tarde.");
		}

		if (!_passwordHasher.Verify(passwordHash: user.PasswordHash, password: password))
		{
			user.RegisterFailedLoginAttempt();
			_userRepository.Update(user);

			await _unitOfWork.SaveChangesAsync(cancellationToken);

			return Result<AuthenticationResult>.Failure("Login ou senha inválidos.");
		}

		if (!user.IsActive)
		{
			return Result<AuthenticationResult>.Failure("Confirme seu e-mail antes de fazer login.");
		}

		user.RegisterSuccessfulLogin();
		_userRepository.Update(user);

		var authenticationResult = _authenticationResultFactory.IssueFor(user);

		await _unitOfWork.SaveChangesAsync(cancellationToken);

		return Result<AuthenticationResult>.Success(authenticationResult);
	}
}
