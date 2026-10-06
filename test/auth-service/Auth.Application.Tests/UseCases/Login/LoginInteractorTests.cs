namespace Ouroboros.Auth.Application.UseCases.Login;

using Ouroboros.Auth.Application.Fakes;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.Settings;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class LoginInteractorTests
{
    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<bool> RecordFailedAccessAsync(
            Guid externalId,
            DateTimeOffset now)
        {
            var user = Items.Single(item => item.ExternalId == externalId);
            return Task.FromResult(user.RecordFailedAccess(now));
        }

        public Task<bool> TryRehashPasswordAsync(
            Guid externalId,
            string currentPasswordHash,
            string newPasswordHash)
        {
            Rehashes.Add((externalId, currentPasswordHash, newPasswordHash));
            return Task.FromResult(RehashSucceeds);
        }

        public List<(Guid ExternalId, string CurrentHash, string NewHash)> Rehashes { get; } = new();

        public bool RehashSucceeds { get; set; } = true;

        public Task<bool> TryRegisterLoginAsync(
            Guid externalId,
            DateTimeOffset now)
        {
            var user = Items.Single(item => item.ExternalId == externalId);

            if (RegisterLoginFails || user.IsLockedOut(now))
            {
                return Task.FromResult(false);
            }

            user.RegisterLogin(now);
            return Task.FromResult(true);
        }

        public bool RegisterLoginFails { get; set; }

        public Task ClearLockoutAsync(
            Guid externalId,
            DateTimeOffset now)
        {
            throw new NotSupportedException();
        }
        public Task<bool> TryResetFailedAccessAsync(
            Guid externalId,
            DateTimeOffset now)
        {
            var user = Items.Single(item => item.ExternalId == externalId);

            if (user.IsLockedOut(now))
            {
                return Task.FromResult(false);
            }

            user.ResetFailedAccess();
            return Task.FromResult(true);
        }
        public List<User> Items { get; } = new();

        public Task AddAsync(User user)
        {
            Items.Add(user);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid externalId)
        {
            return Task.CompletedTask;
        }

        public Task<bool> ExistsDeletedByLoginAsync(string login)
        {
            return Task.FromResult(false);
        }

        public Task<int> CountActiveAdminsForUpdateAsync()
        {
            return Task.FromResult(0);
        }

        public Task<User?> GetByExternalIdAsync(Guid externalId)
        {
            var user = Items.FirstOrDefault(item => item.ExternalId == externalId);
            return Task.FromResult(user);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            var user = Items.FirstOrDefault(item => item.NormalizedEmail == email);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginAsync(string login)
        {
            var user = Items.FirstOrDefault(item => item.NormalizedLogin == login);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginOrEmailAsync(string loginOrEmail)
        {
            var user = Items.FirstOrDefault(item => item.NormalizedLogin == loginOrEmail || item.NormalizedEmail == loginOrEmail);
            return Task.FromResult(user);
        }

        public Task UpdateAsync(User user)
        {
            return Task.CompletedTask;
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

        public Task RevokeAllActiveByUserExceptSessionAsync(
            Guid userExternalId,
            Guid exceptSessionId,
            DateTimeOffset revokedAt)
        {
            foreach (var token in Items.Where(item => item.UserExternalId == userExternalId && item.SessionId != exceptSessionId && item.IsActive(revokedAt)))
            {
                token.Revoke(revokedAt);
            }

            return Task.CompletedTask;
        }

        public Task<int> DeleteExpiredBatchAsync(DateTimeOffset expiredBefore, int batchSize)
        {
            throw new NotSupportedException();
        }

        public Task RevokeAllActiveBySessionAsync(Guid sessionId, DateTimeOffset revokedAt)
        {
            SessionRevocations.Add(sessionId);
            return Task.CompletedTask;
        }

        public List<Guid> SessionRevocations { get; } = new();

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
        public string DummyHash => "hashed:dummy";
        public List<string> VerifiedHashes { get; } = new();

        public string Hash(string password)
        {
            return $"hashed:{password}";
        }

        public bool RehashNeeded { get; set; }

        public bool NeedsRehash(string passwordHash)
        {
            return RehashNeeded;
        }

        public bool Verify(string password, string passwordHash)
        {
            VerifiedHashes.Add(passwordHash);
            return passwordHash == Hash(password);
        }
    }

    private sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
    {
        public static readonly DateTimeOffset ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        public Guid? LastSessionId { get; private set; }

        public AccessToken Generate(
            Guid userId,
            string login,
            string email,
            UserRole role,
            Guid sessionId)
        {
            LastSessionId = sessionId;
            return new AccessToken($"jwt:{userId}:{login}:{email}:{role}", ExpiresAt);
        }
    }

    private sealed class FakeTokenGenerator : ITokenGenerator
    {
        public string Generate()
        {
            return "raw-refresh-token";
        }

        public string Hash(string token)
        {
            return $"hashed:{token}";
        }
    }

    private static readonly RefreshTokenSettings RefreshTokenSettings = new(TimeSpan.FromDays(7));

    private static User CreateUser(bool active)
    {
        var user = User.Create("jdoe", "John Doe", "jdoe@example.com", "hashed:Str0ng-Passphrase-1");

        if (active)
        {
            user.ConfirmEmail();
        }

        return user;
    }

    private static User CreateActiveUserWithRole(UserRole role)
    {
        return User.Rehydrate(
            1,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(-30),
            null,
            "jdoe",
            "John Doe",
            "jdoe@example.com",
            true,
            "hashed:Str0ng-Passphrase-1",
            DateTimeOffset.UtcNow.AddDays(-30),
            true,
            null,
            role,
            null);
    }

    private static LoginInteractor CreateInteractor(
        FakeUserRepository userRepository,
        FakeRefreshTokenRepository refreshTokenRepository,
        FakePasswordHasher? passwordHasher = null,
        FakeUnitOfWork? unitOfWork = null,
        FakeJwtTokenGenerator? jwtTokenGenerator = null,
        FakeAuditLog? auditLog = null)
    {
        return new LoginInteractor(
            userRepository,
            refreshTokenRepository,
            passwordHasher ?? new FakePasswordHasher(),
            jwtTokenGenerator ?? new FakeJwtTokenGenerator(),
            new FakeTokenGenerator(),
            RefreshTokenSettings,
            unitOfWork ?? new FakeUnitOfWork(),
            auditLog ?? new FakeAuditLog());
    }

    [Fact]
    public async Task ShouldIssueAccessAndRefreshTokensWhenCredentialsAreValid()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        var response = await interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1"));

        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal($"jwt:{user.ExternalId}:jdoe:jdoe@example.com:User", response.AccessToken);
        Assert.Equal(FakeJwtTokenGenerator.ExpiresAt, response.AccessTokenExpiresAt);
        Assert.Equal("raw-refresh-token", response.RefreshToken);
        Assert.Single(refreshTokenRepository.Items);
        Assert.Equal(user.ExternalId, refreshTokenRepository.Items[0].UserExternalId);
        Assert.Equal("hashed:raw-refresh-token", refreshTokenRepository.Items[0].TokenHash);
        Assert.Equal(response.RefreshTokenExpiresAt, refreshTokenRepository.Items[0].ExpiresAt);
        Assert.True(response.RefreshTokenExpiresAt > DateTimeOffset.UtcNow.AddDays(6));
        Assert.Null(refreshTokenRepository.Items[0].RevokedAt);
    }

    [Fact]
    public async Task ShouldIssueAccessTokenWithUserRoleWhenUserIsAdmin()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateActiveUserWithRole(UserRole.Admin);
        userRepository.Items.Add(user);
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        var response = await interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1"));

        Assert.Equal($"jwt:{user.ExternalId}:jdoe:jdoe@example.com:Admin", response.AccessToken);
    }

    [Fact]
    public async Task ShouldKeepPreviousSessionsActiveWhenUserLogsInAgain()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        var previousToken = RefreshToken.Create(user.ExternalId, "hashed:previous", DateTimeOffset.UtcNow.AddDays(1));
        refreshTokenRepository.Items.Add(previousToken);
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1"));

        var newToken = refreshTokenRepository.Items.Single(item => item.TokenHash == "hashed:raw-refresh-token");
        Assert.Null(previousToken.RevokedAt);
        Assert.Null(newToken.RevokedAt);
        Assert.NotEqual(previousToken.SessionId, newToken.SessionId);
    }

    [Fact]
    public async Task ShouldIssueAccessTokenWithTheSessionIdOfTheNewRefreshToken()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var jwtTokenGenerator = new FakeJwtTokenGenerator();
        userRepository.Items.Add(CreateUser(active: true));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository, jwtTokenGenerator: jwtTokenGenerator);

        await interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1"));

        var refreshToken = Assert.Single(refreshTokenRepository.Items);
        Assert.NotEqual(Guid.Empty, refreshToken.SessionId);
        Assert.Equal(refreshToken.SessionId, jwtTokenGenerator.LastSessionId);
    }

    [Fact]
    public async Task ShouldRecordLastLoginAtWhenLoginSucceeds()
    {
        var userRepository = new FakeUserRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        var interactor = CreateInteractor(userRepository, new FakeRefreshTokenRepository());
        var before = DateTimeOffset.UtcNow;

        await interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1"));

        Assert.NotNull(user.LastLoginAt);
        Assert.True(user.LastLoginAt >= before);
    }

    [Fact]
    public async Task ShouldNotRecordLastLoginAtWhenLoginIsRejected()
    {
        var userRepository = new FakeUserRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        var interactor = CreateInteractor(userRepository, new FakeRefreshTokenRepository());

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Wrong!123")));

        Assert.Null(user.LastLoginAt);
    }

    [Fact]
    public async Task ShouldRunAllWritesInsideTheSameUnitOfWork()
    {
        var unitOfWork = new FakeUnitOfWork();
        var userRepository = new FakeUserRepository();
        userRepository.Items.Add(CreateUser(active: true));
        var interactor = CreateInteractor(userRepository, new FakeRefreshTokenRepository(), unitOfWork: unitOfWork);

        await interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1"));

        Assert.Equal(1, unitOfWork.Commits);
        Assert.Equal(0, unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldRollBackAndCreateNoRefreshTokenWhenAccountIsLockedBetweenReadAndWrite()
    {
        var unitOfWork = new FakeUnitOfWork();
        var userRepository = new FakeUserRepository { RegisterLoginFails = true };
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        userRepository.Items.Add(CreateUser(active: true));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository, unitOfWork: unitOfWork);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1")));

        Assert.Empty(refreshTokenRepository.Items);
        Assert.Equal(0, unitOfWork.Commits);
        Assert.Equal(1, unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldNotOpenUnitOfWorkWhenUserIsInactive()
    {
        var unitOfWork = new FakeUnitOfWork();
        var userRepository = new FakeUserRepository();
        userRepository.Items.Add(CreateUser(active: false));
        var interactor = CreateInteractor(userRepository, new FakeRefreshTokenRepository(), unitOfWork: unitOfWork);

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1")));

        Assert.Equal(0, unitOfWork.Commits);
        Assert.Equal(0, unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldNotRevokePreviousRefreshTokensWhenLoginIsRejected()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        var previousToken = RefreshToken.Create(user.ExternalId, "hashed:previous", DateTimeOffset.UtcNow.AddDays(1));
        refreshTokenRepository.Items.Add(previousToken);
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Wrong!123")));
        Assert.Null(previousToken.RevokedAt);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenUserIsInactive()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        userRepository.Items.Add(CreateUser(active: false));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        var exception = await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1")));

        Assert.IsNotType<InvalidCredentialsException>(exception);
        Assert.Empty(refreshTokenRepository.Items);
    }

    [Fact]
    public async Task ShouldThrowInvalidCredentialsExceptionWhenPasswordIsIncorrect()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        userRepository.Items.Add(CreateUser(active: true));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Wrong!123")));
        Assert.Empty(refreshTokenRepository.Items);
    }

    [Fact]
    public async Task ShouldThrowInvalidCredentialsExceptionWhenLoginDoesNotExist()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        userRepository.Items.Add(CreateUser(active: true));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("unknown", "Str0ng-Passphrase-1")));
        Assert.Empty(refreshTokenRepository.Items);
    }

    [Fact]
    public async Task ShouldThrowInvalidCredentialsExceptionWhenCredentialsAreBlank()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        userRepository.Items.Add(CreateUser(active: true));
        var interactor = CreateInteractor(userRepository, refreshTokenRepository);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("   ", "")));
        Assert.Empty(refreshTokenRepository.Items);
    }

    [Fact]
    public async Task ShouldUseDummyHashOnceWhenLoginDoesNotExist()
    {
        var users = new FakeUserRepository();
        var hasher = new FakePasswordHasher();
        var interactor = CreateInteractor(users, new FakeRefreshTokenRepository(), hasher);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            interactor.ExecuteAsync(new LoginRequest("unknown", "Wrong!123")));

        Assert.Equal(hasher.DummyHash, Assert.Single(hasher.VerifiedHashes));
    }

    [Fact]
    public async Task ShouldUseDummyHashAndGenericErrorWhenAccountIsLocked()
    {
        var users = new FakeUserRepository();
        var user = CreateUser(active: true);
        users.Items.Add(user);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            user.RecordFailedAccess(DateTimeOffset.UtcNow);
        }

        var hasher = new FakePasswordHasher();
        var interactor = CreateInteractor(users, new FakeRefreshTokenRepository(), hasher);

        var error = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1")));

        Assert.Equal("Invalid login or password", error.Message);
        Assert.Equal(hasher.DummyHash, Assert.Single(hasher.VerifiedHashes));
    }

    [Fact]
    public async Task ShouldResetFailureCountAfterSuccessfulLogin()
    {
        var users = new FakeUserRepository();
        var user = CreateUser(active: true);
        users.Items.Add(user);
        user.RecordFailedAccess(DateTimeOffset.UtcNow);
        var interactor = CreateInteractor(users, new FakeRefreshTokenRepository());

        await interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1"));

        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public async Task ShouldRehashPasswordWhenStoredHashHasLowerCost()
    {
        var users = new FakeUserRepository();
        var user = CreateUser(active: true);
        users.Items.Add(user);
        var hasher = new FakePasswordHasher { RehashNeeded = true };
        var interactor = CreateInteractor(users, new FakeRefreshTokenRepository(), hasher);

        await interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1"));

        var rehash = Assert.Single(users.Rehashes);
        Assert.Equal(user.ExternalId, rehash.ExternalId);
        Assert.Equal("hashed:Str0ng-Passphrase-1", rehash.CurrentHash);
        Assert.Equal("hashed:Str0ng-Passphrase-1", rehash.NewHash);
    }

    [Fact]
    public async Task ShouldNotRehashPasswordWhenStoredHashHasCurrentCost()
    {
        var users = new FakeUserRepository();
        users.Items.Add(CreateUser(active: true));
        var interactor = CreateInteractor(users, new FakeRefreshTokenRepository());

        await interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1"));

        Assert.Empty(users.Rehashes);
    }

    [Fact]
    public async Task ShouldNotRehashPasswordWhenLoginIsRejected()
    {
        var users = new FakeUserRepository();
        users.Items.Add(CreateUser(active: true));
        var interactor = CreateInteractor(users, new FakeRefreshTokenRepository(), new FakePasswordHasher { RehashNeeded = true });

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Wrong!123")));

        Assert.Empty(users.Rehashes);
    }

    [Fact]
    public async Task ShouldCompleteLoginWhenRehashLosesTheRaceAgainstAPasswordChange()
    {
        var users = new FakeUserRepository { RehashSucceeds = false };
        users.Items.Add(CreateUser(active: true));
        var interactor = CreateInteractor(users, new FakeRefreshTokenRepository(), new FakePasswordHasher { RehashNeeded = true });

        var response = await interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1"));

        Assert.Equal("raw-refresh-token", response.RefreshToken);
    }

    [Fact]
    public async Task ShouldRecordTheLoginSucceededEventWithTheSessionInsideTheUnitOfWork()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var auditLog = new FakeAuditLog();
        var unitOfWork = new FakeUnitOfWork();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        var interactor = CreateInteractor(userRepository, refreshTokenRepository, unitOfWork: unitOfWork, auditLog: auditLog);

        await interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1"));

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal(AuditEventType.LoginSucceeded, auditEvent.Type);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
        Assert.Equal(user.ExternalId, auditEvent.UserExternalId);
        Assert.Equal(Assert.Single(refreshTokenRepository.Items).SessionId, auditEvent.SessionId);
        Assert.Equal(1, unitOfWork.Commits);
    }

    [Fact]
    public async Task ShouldRecordTheLoginFailedEventWithTheUserAndTheInvalidPasswordReason()
    {
        var userRepository = new FakeUserRepository();
        var auditLog = new FakeAuditLog();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        var interactor = CreateInteractor(userRepository, new FakeRefreshTokenRepository(), auditLog: auditLog);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Wrong!123")));

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal(AuditEventType.LoginFailed, auditEvent.Type);
        Assert.Equal(AuditOutcome.Failure, auditEvent.Outcome);
        Assert.Equal(user.ExternalId, auditEvent.UserExternalId);
        Assert.Equal("invalid_password", auditEvent.Reason);
        Assert.DoesNotContain("Wrong!123", System.Text.Json.JsonSerializer.Serialize(auditEvent));
    }

    [Fact]
    public async Task ShouldNeverRecordTheTypedLoginNorThePasswordWhenTheAccountDoesNotExist()
    {
        var userRepository = new FakeUserRepository();
        userRepository.Items.Add(CreateUser(active: true));
        var auditLog = new FakeAuditLog();
        var interactor = CreateInteractor(userRepository, new FakeRefreshTokenRepository(), auditLog: auditLog);

        // Usuarios digitam a senha no campo de login por engano.
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("my-secret-typed-here", "Wrong!123")));

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal(AuditEventType.LoginFailed, auditEvent.Type);
        Assert.Null(auditEvent.UserExternalId);
        Assert.Equal("unknown_login", auditEvent.Reason);
        var serialized = System.Text.Json.JsonSerializer.Serialize(auditEvent);
        Assert.DoesNotContain("my-secret-typed-here", serialized);
        Assert.DoesNotContain("Wrong!123", serialized);
    }

    [Fact]
    public async Task ShouldRecordTheLoginFailedEventWithTheLockedOutReasonWhenTheAccountIsLocked()
    {
        var userRepository = new FakeUserRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);

        for (var attempt = 0; attempt < User.MaxFailedAccessAttempts; attempt++)
        {
            user.RecordFailedAccess(DateTimeOffset.UtcNow);
        }

        var auditLog = new FakeAuditLog();
        var interactor = CreateInteractor(userRepository, new FakeRefreshTokenRepository(), auditLog: auditLog);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1")));

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal(AuditEventType.LoginFailed, auditEvent.Type);
        Assert.Equal(user.ExternalId, auditEvent.UserExternalId);
        Assert.Equal("locked_out", auditEvent.Reason);
    }

    [Fact]
    public async Task ShouldRecordTheAccountLockedOutEventWhenTheLastAllowedAttemptFails()
    {
        var userRepository = new FakeUserRepository();
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);

        for (var attempt = 0; attempt < User.MaxFailedAccessAttempts - 1; attempt++)
        {
            user.RecordFailedAccess(DateTimeOffset.UtcNow);
        }

        var auditLog = new FakeAuditLog();
        var interactor = CreateInteractor(userRepository, new FakeRefreshTokenRepository(), auditLog: auditLog);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Wrong!123")));

        Assert.True(auditLog.Has(AuditEventType.LoginFailed));
        var locked = auditLog.Single(AuditEventType.AccountLockedOut);
        Assert.Equal(AuditOutcome.Failure, locked.Outcome);
        Assert.Equal(user.ExternalId, locked.UserExternalId);
        Assert.Equal("too_many_failed_attempts", locked.Reason);
    }

    [Fact]
    public async Task ShouldRecordTheLoginFailedEventWithTheInactiveAccountReason()
    {
        var userRepository = new FakeUserRepository();
        var user = CreateUser(active: false);
        userRepository.Items.Add(user);
        var auditLog = new FakeAuditLog();
        var interactor = CreateInteractor(userRepository, new FakeRefreshTokenRepository(), auditLog: auditLog);

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1")));

        var auditEvent = Assert.Single(auditLog.Events);
        Assert.Equal(AuditEventType.LoginFailed, auditEvent.Type);
        Assert.Equal("inactive_account", auditEvent.Reason);
    }

    [Fact]
    public async Task ShouldRecordOnlyTheFailureAndNoSuccessWhenTheAccountIsLockedBetweenReadAndWrite()
    {
        var userRepository = new FakeUserRepository { RegisterLoginFails = true };
        var user = CreateUser(active: true);
        userRepository.Items.Add(user);
        var auditLog = new FakeAuditLog();
        var interactor = CreateInteractor(userRepository, new FakeRefreshTokenRepository(), auditLog: auditLog);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1")));

        Assert.False(auditLog.Has(AuditEventType.LoginSucceeded));
        Assert.Equal("locked_out", auditLog.Single(AuditEventType.LoginFailed).Reason);
    }

    [Fact]
    public async Task ShouldRollBackAndIssueNoTokenWhenTheSuccessEventCannotBeRecorded()
    {
        var userRepository = new FakeUserRepository();
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        userRepository.Items.Add(CreateUser(active: true));
        var interactor = CreateInteractor(
            userRepository,
            refreshTokenRepository,
            unitOfWork: unitOfWork,
            auditLog: new FakeAuditLog { RecordException = new InvalidOperationException("audit failed") });

        await Assert.ThrowsAsync<InvalidOperationException>(() => interactor.ExecuteAsync(new LoginRequest("jdoe", "Str0ng-Passphrase-1")));

        Assert.Equal(0, unitOfWork.Commits);
        Assert.Equal(1, unitOfWork.Rollbacks);
    }
}
