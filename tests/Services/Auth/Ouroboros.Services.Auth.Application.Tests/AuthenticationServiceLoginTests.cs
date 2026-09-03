namespace Ouroboros.Services.Auth.Application.Tests;

public class AuthenticationServiceLoginTests
{
	private const string CorrectPassword = "correct-password";

	[Fact]
	public async Task LoginAsync_WithCorrectCredentials_ReturnsAccessTokenAndResetsAttempts()
	{
		var context = new AuthTestContext();
		var user = context.AddUser(password: CorrectPassword);
		user.RegisterFailedLoginAttempt();

		var authenticationService = context.CreateAuthenticationService();

		var result = await authenticationService.LoginAsync("jsilva", CorrectPassword, CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal("token-for:jsilva", result.Value!.AccessToken);
		Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));

		Assert.Equal(0, user.FailedLoginAttempts);
		Assert.NotNull(user.LastLoginAt);

		var refreshToken = Assert.Single(context.RefreshTokenRepository.RefreshTokens);
		Assert.Same(user, refreshToken.User);
		Assert.Null(refreshToken.RevokedAt);
	}

	[Fact]
	public async Task LoginAsync_WithUnknownLogin_ReturnsFailure()
	{
		var context = new AuthTestContext();
		var authenticationService = context.CreateAuthenticationService();

		var result = await authenticationService.LoginAsync("nao-existe", "any-password", CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Empty(context.RefreshTokenRepository.RefreshTokens);
	}

	[Fact]
	public async Task LoginAsync_WithWrongPassword_ReturnsFailureAndRegistersAttempt()
	{
		var context = new AuthTestContext();
		var user = context.AddUser(password: CorrectPassword);

		var authenticationService = context.CreateAuthenticationService();

		var result = await authenticationService.LoginAsync("jsilva", "senha-errada", CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Equal(1, user.FailedLoginAttempts);
		Assert.Empty(context.RefreshTokenRepository.RefreshTokens);
	}

	[Fact]
	public async Task LoginAsync_WithInactiveUser_ReturnsFailure()
	{
		var context = new AuthTestContext();
		context.AddUser(password: CorrectPassword, confirmEmail: false);

		var authenticationService = context.CreateAuthenticationService();

		var result = await authenticationService.LoginAsync("jsilva", CorrectPassword, CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Empty(context.RefreshTokenRepository.RefreshTokens);
	}

	[Fact]
	public async Task LoginAsync_WithLockedUser_ReturnsFailureWithoutCheckingPassword()
	{
		var context = new AuthTestContext();
		var user = context.AddUser(password: CorrectPassword);

		for (var attempt = 0; attempt < 5; attempt++)
		{
			user.RegisterFailedLoginAttempt();
		}

		var authenticationService = context.CreateAuthenticationService();

		var result = await authenticationService.LoginAsync("jsilva", CorrectPassword, CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Empty(context.RefreshTokenRepository.RefreshTokens);
	}
}
