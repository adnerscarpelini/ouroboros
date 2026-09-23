namespace Ouroboros.Auth.Application.UseCases.ResetPassword;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class ResetPasswordInteractorTests
{
    private const string CurrentPassword = "Current@123";
    private const string NewPassword = "NewPass@456";
    private const string InvalidTokenMessage = "Invalid or expired password reset token";

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Items { get; } = new();

        public List<User> Updated { get; } = new();

        public Task AddAsync(User user)
        {
            Items.Add(user);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsByLoginOrEmailAsync(string login, string email)
        {
            var exists = Items.Any(item => item.Login == login || item.Email == email);
            return Task.FromResult(exists);
        }

        public Task<User?> GetByExternalIdAsync(Guid externalId)
        {
            var user = Items.FirstOrDefault(item => item.ExternalId == externalId);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginAsync(string login)
        {
            var user = Items.FirstOrDefault(item => item.Login == login);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginOrEmailAsync(string loginOrEmail)
        {
            var user = Items.FirstOrDefault(item => item.Login == loginOrEmail || item.Email == loginOrEmail);
            return Task.FromResult(user);
        }

        public Task UpdateAsync(User user)
        {
            Updated.Add(user);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenRepository : ITokenRepository
    {
        public List<Token> Items { get; } = new();

        public List<Token> MarkedAsUsed { get; } = new();

        public bool ConcurrentUse { get; set; }

        public Task AddAsync(Token token)
        {
            Items.Add(token);
            return Task.CompletedTask;
        }

        public Task<Token?> GetByHashAsync(string tokenHash, TokenType type)
        {
            var token = Items.FirstOrDefault(item => item.TokenHash == tokenHash && item.Type == type);
            return Task.FromResult(token);
        }

        public Task UpdateAsync(Token token)
        {
            return Task.CompletedTask;
        }

        public Task InvalidatePendingByUserAsync(
            Guid userExternalId,
            TokenType type,
            DateTimeOffset invalidatedAt)
        {
            return Task.CompletedTask;
        }

        // Simula outra requisicao que usou o mesmo token antes desta gravar.
        public Task<bool> TryMarkAsUsedAsync(Token token)
        {
            if (ConcurrentUse)
            {
                return Task.FromResult(false);
            }

            MarkedAsUsed.Add(token);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Items { get; } = new();

        public Task AddAsync(RefreshToken refreshToken)
        {
            Items.Add(refreshToken);
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> GetByHashAsync(string tokenHash)
        {
            var refreshToken = Items.FirstOrDefault(item => item.TokenHash == tokenHash);
            return Task.FromResult(refreshToken);
        }

        public Task<bool> TryRevokeAsync(RefreshToken refreshToken)
        {
            return Task.FromResult(true);
        }

        public Task RevokeAllActiveByUserAsync(Guid userExternalId, DateTimeOffset revokedAt)
        {
            var activeTokens = Items.Where(item => item.UserExternalId == userExternalId && item.IsActive(revokedAt));

            foreach (var activeToken in activeTokens)
            {
                activeToken.Revoke(revokedAt);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password)
        {
            return $"hashed:{password}";
        }

        public bool Verify(string password, string passwordHash)
        {
            return passwordHash == Hash(password);
        }
    }

    private sealed class FakeTokenGenerator : ITokenGenerator
    {
        public string Generate()
        {
            return "raw-token";
        }

        public string Hash(string token)
        {
            return $"hashed:{token}";
        }
    }

    private sealed class Scenario
    {
        public FakeUserRepository UserRepository { get; } = new();

        public FakeTokenRepository TokenRepository { get; } = new();

        public FakeRefreshTokenRepository RefreshTokenRepository { get; } = new();

        public User User { get; }

        public DateTimeOffset OriginalPasswordChangedAt { get; }

        public Scenario(bool userActive = true)
        {
            OriginalPasswordChangedAt = DateTimeOffset.UtcNow.AddDays(-30);
            User = User.Rehydrate(
                1,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddDays(-60),
                null,
                "jdoe",
                "John Doe",
                "jdoe@example.com",
                true,
                $"hashed:{CurrentPassword}",
                OriginalPasswordChangedAt,
                userActive,
                null);
            UserRepository.Items.Add(User);
        }

        public Token AddToken(
            TokenType type,
            DateTimeOffset expiresAt,
            DateTimeOffset? usedAt)
        {
            var token = Token.Rehydrate(
                1,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddMinutes(-10),
                null,
                User.ExternalId,
                type,
                "hashed:raw-token",
                expiresAt,
                usedAt);
            TokenRepository.Items.Add(token);

            return token;
        }

        public Token AddPendingToken()
        {
            return AddToken(TokenType.PasswordReset, DateTimeOffset.UtcNow.AddMinutes(50), null);
        }

        public RefreshToken AddActiveRefreshToken()
        {
            var refreshToken = RefreshToken.Create(User.ExternalId, $"refresh-{Guid.NewGuid()}", DateTimeOffset.UtcNow.AddDays(7));
            RefreshTokenRepository.Items.Add(refreshToken);

            return refreshToken;
        }

        public ResetPasswordInteractor CreateInteractor()
        {
            return new ResetPasswordInteractor(
                TokenRepository,
                UserRepository,
                RefreshTokenRepository,
                new FakePasswordHasher(),
                new FakeTokenGenerator());
        }

        public void AssertNothingChanged()
        {
            Assert.Empty(UserRepository.Updated);
            Assert.Empty(TokenRepository.MarkedAsUsed);
            Assert.Equal($"hashed:{CurrentPassword}", User.PasswordHash);
            Assert.All(RefreshTokenRepository.Items, item => Assert.Null(item.RevokedAt));
        }
    }

    [Fact]
    public async Task ShouldChangePasswordWhenTokenIsValid()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();

        var response = await scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword));

        Assert.Equal(scenario.User.ExternalId, response.UserId);
        var updatedUser = Assert.Single(scenario.UserRepository.Updated);
        Assert.Equal($"hashed:{NewPassword}", updatedUser.PasswordHash);
        Assert.True(updatedUser.PasswordChangedAt > scenario.OriginalPasswordChangedAt);
        Assert.NotNull(updatedUser.UpdatedAt);
    }

    [Fact]
    public async Task ShouldMarkTokenAsUsedWhenPasswordIsChanged()
    {
        var scenario = new Scenario();
        var token = scenario.AddPendingToken();

        await scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword));

        Assert.Same(token, Assert.Single(scenario.TokenRepository.MarkedAsUsed));
        Assert.NotNull(token.UsedAt);
    }

    [Fact]
    public async Task ShouldRevokeAllActiveRefreshTokensWhenPasswordIsChanged()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();
        scenario.AddActiveRefreshToken();

        await scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword));

        Assert.All(scenario.RefreshTokenRepository.Items, item => Assert.NotNull(item.RevokedAt));
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenDoesNotExist()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("unknown-token", NewPassword)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenIsBlank()
    {
        var scenario = new Scenario();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("   ", NewPassword)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenIsExpired()
    {
        var scenario = new Scenario();
        scenario.AddToken(TokenType.PasswordReset, DateTimeOffset.UtcNow.AddMinutes(-1), null);
        scenario.AddActiveRefreshToken();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenWasAlreadyUsed()
    {
        var scenario = new Scenario();
        scenario.AddToken(TokenType.PasswordReset, DateTimeOffset.UtcNow.AddMinutes(50), DateTimeOffset.UtcNow.AddMinutes(-5));
        scenario.AddActiveRefreshToken();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenIsEmailConfirmation()
    {
        var scenario = new Scenario();
        scenario.AddToken(TokenType.EmailConfirmation, DateTimeOffset.UtcNow.AddHours(20), null);
        scenario.AddActiveRefreshToken();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenTokenIsUsedConcurrently()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();
        scenario.TokenRepository.ConcurrentUse = true;

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        Assert.Equal(InvalidTokenMessage, exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenUserIsInactive()
    {
        var scenario = new Scenario(userActive: false);
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();

        await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", NewPassword)));

        scenario.AssertNothingChanged();
    }

    [Theory]
    [InlineData("Ab@1")]
    [InlineData("newpass@456")]
    [InlineData("NEWPASS@456")]
    [InlineData("NewPass@abc")]
    [InlineData("NewPass4567")]
    public async Task ShouldThrowDomainExceptionWhenNewPasswordIsWeak(string weakPassword)
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();

        await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", weakPassword)));

        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenNewPasswordEqualsCurrentPassword()
    {
        var scenario = new Scenario();
        scenario.AddPendingToken();
        scenario.AddActiveRefreshToken();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", CurrentPassword)));

        Assert.Equal("New password must be different from the current password", exception.Message);
        scenario.AssertNothingChanged();
    }

    [Fact]
    public async Task ShouldKeepTokenPendingWhenNewPasswordIsRejected()
    {
        var scenario = new Scenario();
        var token = scenario.AddPendingToken();

        await Assert.ThrowsAsync<DomainException>(() =>
            scenario.CreateInteractor().ExecuteAsync(new ResetPasswordRequest("raw-token", "weak")));

        Assert.True(token.IsPending(DateTimeOffset.UtcNow));
    }
}
