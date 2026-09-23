namespace Ouroboros.Auth.Application.UseCases.DeleteUser;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class DeleteUserInteractorTests
{
    private const string Password = "S3cret!1";

    // Guarda tambem contas excluidas e as ignora nas buscas, igual ao repositorio real.
    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Items { get; } = new();

        public List<User> Updated { get; } = new();

        public int LookupCount { get; private set; }

        public Task AddAsync(User user)
        {
            Items.Add(user);
            return Task.CompletedTask;
        }

        public Task<User?> GetByExternalIdAsync(Guid externalId)
        {
            LookupCount++;
            var user = Items.FirstOrDefault(item => item.DeletedAt is null && item.ExternalId == externalId);
            return Task.FromResult(user);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            LookupCount++;
            var user = Items.FirstOrDefault(item => item.DeletedAt is null && item.Email == email);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginAsync(string login)
        {
            LookupCount++;
            var user = Items.FirstOrDefault(item => item.DeletedAt is null && item.Login == login);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginOrEmailAsync(string loginOrEmail)
        {
            LookupCount++;
            var user = Items.FirstOrDefault(item => item.DeletedAt is null && (item.Login == loginOrEmail || item.Email == loginOrEmail));
            return Task.FromResult(user);
        }

        public Task UpdateAsync(User user)
        {
            Updated.Add(user);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid externalId)
        {
            return Task.CompletedTask;
        }

        public Task<bool> ExistsDeletedByLoginAsync(string login)
        {
            var exists = Items.Any(item => item.DeletedAt is not null && item.Login == login);
            return Task.FromResult(exists);
        }

        public Task<int> CountActiveAdminsAsync()
        {
            var count = Items.Count(item => item.Role == UserRole.Admin && item.Active && item.DeletedAt is null);
            return Task.FromResult(count);
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
            return passwordHash == $"hashed:{password}";
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

    private sealed class FakeTokenRepository : ITokenRepository
    {
        public List<(Guid UserExternalId, TokenType Type)> Invalidated { get; } = new();

        public Task AddAsync(Token token)
        {
            return Task.CompletedTask;
        }

        public Task<Token?> GetByHashAsync(string tokenHash, TokenType type)
        {
            return Task.FromResult<Token?>(null);
        }

        public Task<bool> ExistsPendingByUserAsync(
            Guid userExternalId,
            TokenType type,
            DateTimeOffset now)
        {
            return Task.FromResult(false);
        }

        public Task UpdateAsync(Token token)
        {
            return Task.CompletedTask;
        }

        public Task<bool> TryMarkAsUsedAsync(Token token)
        {
            return Task.FromResult(true);
        }

        public Task InvalidatePendingByUserAsync(
            Guid userExternalId,
            TokenType type,
            DateTimeOffset invalidatedAt)
        {
            Invalidated.Add((userExternalId, type));
            return Task.CompletedTask;
        }
    }

    private sealed class Context
    {
        public FakeUserRepository UserRepository { get; } = new();

        public FakeRefreshTokenRepository RefreshTokenRepository { get; } = new();

        public FakeTokenRepository TokenRepository { get; } = new();

        public DeleteUserInteractor CreateInteractor()
        {
            return new DeleteUserInteractor(
                UserRepository,
                new FakePasswordHasher(),
                RefreshTokenRepository,
                TokenRepository);
        }

        public User AddUser(
            string login,
            UserRole role,
            string password = Password)
        {
            var user = User.Rehydrate(
                UserRepository.Items.Count + 1,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddDays(-30),
                null,
                login,
                "Full Name",
                $"{login}@example.com",
                true,
                $"hashed:{password}",
                DateTimeOffset.UtcNow.AddDays(-30),
                true,
                null,
                role,
                null);

            UserRepository.Items.Add(user);

            return user;
        }
    }

    [Fact]
    public async Task ShouldDeleteOwnAccountWhenPasswordIsCorrect()
    {
        var context = new Context();
        var user = context.AddUser("jdoe", UserRole.User);

        var response = await context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(user.ExternalId, "User", Password, user.ExternalId));

        Assert.Equal(user.ExternalId, response.UserId);
        Assert.NotNull(user.DeletedAt);
        Assert.False(user.Active);
        Assert.Same(user, Assert.Single(context.UserRepository.Updated));
    }

    [Fact]
    public async Task ShouldRevokeActiveRefreshTokensWhenUserIsDeleted()
    {
        var context = new Context();
        var user = context.AddUser("jdoe", UserRole.User);
        var refreshToken = RefreshToken.Create(user.ExternalId, "hashed:refresh", DateTimeOffset.UtcNow.AddDays(7));
        context.RefreshTokenRepository.Items.Add(refreshToken);

        await context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(user.ExternalId, "User", Password, user.ExternalId));

        Assert.NotNull(refreshToken.RevokedAt);
    }

    [Fact]
    public async Task ShouldInvalidatePendingTokensWhenUserIsDeleted()
    {
        var context = new Context();
        var user = context.AddUser("jdoe", UserRole.User);

        await context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(user.ExternalId, "User", Password, user.ExternalId));

        Assert.Contains((user.ExternalId, TokenType.EmailConfirmation), context.TokenRepository.Invalidated);
        Assert.Contains((user.ExternalId, TokenType.PasswordReset), context.TokenRepository.Invalidated);
    }

    [Fact]
    public async Task ShouldThrowInvalidCredentialsExceptionWhenPasswordIsWrong()
    {
        var context = new Context();
        var user = context.AddUser("jdoe", UserRole.User);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(user.ExternalId, "User", "Wr0ng!pass", user.ExternalId)));

        Assert.Null(user.DeletedAt);
        Assert.Empty(context.UserRepository.Updated);
        Assert.Empty(context.TokenRepository.Invalidated);
    }

    [Fact]
    public async Task ShouldThrowInvalidCredentialsExceptionWhenPasswordIsEmpty()
    {
        var context = new Context();
        var user = context.AddUser("jdoe", UserRole.User);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(user.ExternalId, "User", string.Empty, user.ExternalId)));

        Assert.Null(user.DeletedAt);
        Assert.Empty(context.UserRepository.Updated);
    }

    [Fact]
    public async Task ShouldDenyWithoutQueryingRepositoryWhenUserDeletesAnotherAccount()
    {
        var context = new Context();
        var requester = context.AddUser("jdoe", UserRole.User);
        var other = context.AddUser("other", UserRole.User);

        await Assert.ThrowsAsync<AccessDeniedException>(() => context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(requester.ExternalId, "User", Password, other.ExternalId)));

        Assert.Equal(0, context.UserRepository.LookupCount);
        Assert.Null(other.DeletedAt);
    }

    [Fact]
    public async Task ShouldDenyWhenRoleIsNotExactlyAdmin()
    {
        var context = new Context();
        var requester = context.AddUser("jdoe", UserRole.Admin);
        var other = context.AddUser("other", UserRole.User);

        await Assert.ThrowsAsync<AccessDeniedException>(() => context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(requester.ExternalId, "admin", Password, other.ExternalId)));

        Assert.Null(other.DeletedAt);
    }

    [Fact]
    public async Task ShouldDeleteAnotherAccountWhenRequesterIsAdmin()
    {
        var context = new Context();
        var admin = context.AddUser("admin", UserRole.Admin, "Adm1n!pass");
        var user = context.AddUser("jdoe", UserRole.User);

        var response = await context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(admin.ExternalId, "Admin", "Adm1n!pass", user.ExternalId));

        Assert.Equal(user.ExternalId, response.UserId);
        Assert.NotNull(user.DeletedAt);
        Assert.Null(admin.DeletedAt);
        Assert.Same(user, Assert.Single(context.UserRepository.Updated));
    }

    [Fact]
    public async Task ShouldDeleteAnotherAdminWhenRequesterIsAdmin()
    {
        var context = new Context();
        var admin = context.AddUser("admin", UserRole.Admin);
        var otherAdmin = context.AddUser("former-admin", UserRole.Admin);

        await context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(admin.ExternalId, "Admin", Password, otherAdmin.ExternalId));

        Assert.NotNull(otherAdmin.DeletedAt);
        Assert.Null(admin.DeletedAt);
    }

    [Fact]
    public async Task ShouldRequireRequesterPasswordWhenAdminDeletesAnotherAccount()
    {
        var context = new Context();
        var admin = context.AddUser("admin", UserRole.Admin, "Adm1n!pass");
        var user = context.AddUser("jdoe", UserRole.User, "Us3r!pass");

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(admin.ExternalId, "Admin", "Us3r!pass", user.ExternalId)));

        Assert.Null(user.DeletedAt);
        Assert.Empty(context.UserRepository.Updated);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenDeletingLastActiveAdmin()
    {
        var context = new Context();
        var admin = context.AddUser("admin", UserRole.Admin);

        var exception = await Assert.ThrowsAsync<DomainException>(() => context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(admin.ExternalId, "Admin", Password, admin.ExternalId)));

        Assert.Equal("The last active admin cannot be deleted", exception.Message);
        Assert.Null(admin.DeletedAt);
        Assert.True(admin.Active);
        Assert.Empty(context.UserRepository.Updated);
        Assert.Empty(context.TokenRepository.Invalidated);
    }

    [Fact]
    public async Task ShouldDeleteOwnAdminAccountWhenAnotherActiveAdminExists()
    {
        var context = new Context();
        var admin = context.AddUser("admin", UserRole.Admin);
        context.AddUser("other-admin", UserRole.Admin);

        await context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(admin.ExternalId, "Admin", Password, admin.ExternalId));

        Assert.NotNull(admin.DeletedAt);
    }

    [Fact]
    public async Task ShouldThrowUserNotFoundExceptionWhenTargetDoesNotExist()
    {
        var context = new Context();
        var admin = context.AddUser("admin", UserRole.Admin);

        await Assert.ThrowsAsync<UserNotFoundException>(() => context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(admin.ExternalId, "Admin", Password, Guid.NewGuid())));

        Assert.Empty(context.UserRepository.Updated);
    }

    [Fact]
    public async Task ShouldThrowUserNotFoundExceptionWhenTargetIsAlreadyDeleted()
    {
        var context = new Context();
        var admin = context.AddUser("admin", UserRole.Admin);
        var deleted = context.AddUser("jdoe", UserRole.User);
        deleted.Delete();

        await Assert.ThrowsAsync<UserNotFoundException>(() => context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(admin.ExternalId, "Admin", Password, deleted.ExternalId)));

        Assert.Empty(context.UserRepository.Updated);
    }

    [Fact]
    public async Task ShouldThrowInvalidCredentialsExceptionWhenRequesterIsAlreadyDeleted()
    {
        var context = new Context();
        var user = context.AddUser("jdoe", UserRole.User);
        user.Delete();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => context.CreateInteractor().ExecuteAsync(new DeleteUserRequest(user.ExternalId, "User", Password, user.ExternalId)));

        Assert.Empty(context.UserRepository.Updated);
    }
}
