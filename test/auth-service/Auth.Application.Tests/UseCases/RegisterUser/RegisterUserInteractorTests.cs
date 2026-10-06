namespace Ouroboros.Auth.Application.UseCases.RegisterUser;

using Ouroboros.Auth.Application.Fakes;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class RegisterUserInteractorTests
{
    // Guarda tambem contas excluidas e as ignora nas buscas, igual ao repositorio real.
    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<bool> RecordFailedAccessAsync(
            Guid externalId,
            DateTimeOffset now)
        {
            throw new NotSupportedException();
        }
        public Task<bool> TryRehashPasswordAsync(
            Guid externalId,
            string currentPasswordHash,
            string newPasswordHash)
        {
            throw new NotSupportedException();
        }

        public Task<bool> TryRegisterLoginAsync(
            Guid externalId,
            DateTimeOffset now)
        {
            throw new NotSupportedException();
        }

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
            throw new NotSupportedException();
        }
        public List<User> Items { get; } = new();

        // Simula o indice unico barrando o insert num cadastro simultaneo.
        public Exception? AddException { get; set; }

        public Task AddAsync(User user)
        {
            if (AddException is not null)
            {
                throw AddException;
            }

            Items.Add(user);
            return Task.CompletedTask;
        }

        public Task<User?> GetByExternalIdAsync(Guid externalId)
        {
            var user = Items.FirstOrDefault(item => item.DeletedAt is null && item.ExternalId == externalId);
            return Task.FromResult(user);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            var user = Items.FirstOrDefault(item => item.DeletedAt is null && item.NormalizedEmail == email);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginAsync(string login)
        {
            var user = Items.FirstOrDefault(item => item.DeletedAt is null && item.NormalizedLogin == login);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginOrEmailAsync(string loginOrEmail)
        {
            var user = Items.FirstOrDefault(item => item.DeletedAt is null && (item.NormalizedLogin == loginOrEmail || item.NormalizedEmail == loginOrEmail));
            return Task.FromResult(user);
        }

        public Task UpdateAsync(User user)
        {
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid externalId)
        {
            Items.RemoveAll(item => item.DeletedAt is null && item.ExternalId == externalId);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsDeletedByLoginAsync(string login)
        {
            var exists = Items.Any(item => item.DeletedAt is not null && item.NormalizedLogin == login);
            return Task.FromResult(exists);
        }

        public Task<int> CountActiveAdminsForUpdateAsync()
        {
            return Task.FromResult(0);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string DummyHash => "hashed:dummy";
        public string Hash(string password)
        {
            return $"hashed:{password}";
        }

        public bool NeedsRehash(string passwordHash)
        {
            return false;
        }

        public bool Verify(string password, string passwordHash)
        {
            return passwordHash == Hash(password);
        }
    }

    private sealed class FakeTokenRepository : ITokenRepository
    {
        public List<Token> Items { get; } = new();

        public Exception? AddException { get; set; }

        public Task AddAsync(Token token)
        {
            if (AddException is not null)
            {
                throw AddException;
            }

            Items.Add(token);
            return Task.CompletedTask;
        }

        public Task<Token?> GetByHashAsync(string tokenHash, TokenType type)
        {
            var token = Items.FirstOrDefault(item => item.TokenHash == tokenHash && item.Type == type);
            return Task.FromResult(token);
        }

        public Task<bool> ExistsPendingByUserAsync(
            Guid userExternalId,
            TokenType type,
            DateTimeOffset now)
        {
            var exists = Items.Any(item => item.UserExternalId == userExternalId && item.Type == type && item.IsPending(now));
            return Task.FromResult(exists);
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

        public Task<bool> TryMarkAsUsedAsync(Token token)
        {
            return Task.FromResult(true);
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

    private static User CreateExistingUser(
        string login,
        string email)
    {
        return User.Create(login, "Existing User", email, "hashed:Str0ng-Passphrase-1");
    }

    private static Token CreateConfirmationToken(
        Guid userExternalId,
        DateTimeOffset expiresAt)
    {
        return Token.Rehydrate(
            1,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(-2),
            null,
            userExternalId,
            TokenType.EmailConfirmation,
            "hashed:existing-token",
            expiresAt,
            null);
    }

    [Fact]
    public async Task ShouldRegisterUserWithUserRoleWhenUserIsRegistered()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1"));

        Assert.Equal(UserRole.User, userRepository.Items[0].Role);
    }

    [Fact]
    public async Task ShouldGenerateEmailConfirmationTokenWhenUserIsRegistered()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1"));

        Assert.Equal("raw-token", response.EmailConfirmationToken);
        Assert.Single(tokenRepository.Items);
        Assert.Equal(response.UserId, tokenRepository.Items[0].UserExternalId);
        Assert.Equal(TokenType.EmailConfirmation, tokenRepository.Items[0].Type);
        Assert.Equal("hashed:raw-token", tokenRepository.Items[0].TokenHash);
        Assert.True(tokenRepository.Items[0].ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Null(tokenRepository.Items[0].UsedAt);
    }

    [Fact]
    public async Task ShouldNotGenerateTokenWhenRegistrationIsRejected()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "not-an-email", "Str0ng-Passphrase-1")));
        Assert.Empty(tokenRepository.Items);
    }

    [Fact]
    public async Task ShouldRegisterUserInactiveWhenDataIsValid()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1"));

        Assert.Single(repository.Items);
        Assert.Equal(repository.Items[0].ExternalId, response.UserId);
        Assert.Equal("jdoe", repository.Items[0].Login);
        Assert.Equal("John Doe", repository.Items[0].FullName);
        Assert.Equal("jdoe@example.com", repository.Items[0].Email);
        Assert.Equal("hashed:Str0ng-Passphrase-1", repository.Items[0].PasswordHash);
        Assert.False(repository.Items[0].Active);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenLoginIsInvalid()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("   ", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenEmailIsInvalid()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "not-an-email", "Str0ng-Passphrase-1")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDuplicateLoginExceptionWhenLoginBelongsToConfirmedUser()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var existing = CreateExistingUser("jdoe", "jdoe@example.com");
        existing.ConfirmEmail();
        userRepository.Items.Add(existing);
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        var exception = await Assert.ThrowsAsync<DuplicateLoginException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "Another Name", "other@example.com", "Str0ng-Passphrase-1")));

        Assert.Equal("Login already in use", exception.Message);
        Assert.Same(existing, Assert.Single(userRepository.Items));
        Assert.Empty(tokenRepository.Items);
    }

    [Fact]
    public async Task ShouldThrowDuplicateLoginExceptionWhenLoginBelongsToPendingRegistration()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var existing = CreateExistingUser("jdoe", "jdoe@example.com");
        userRepository.Items.Add(existing);
        tokenRepository.Items.Add(CreateConfirmationToken(existing.ExternalId, DateTimeOffset.UtcNow.AddHours(1)));
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        await Assert.ThrowsAsync<DuplicateLoginException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "Another Name", "other@example.com", "Str0ng-Passphrase-1")));

        Assert.Same(existing, Assert.Single(userRepository.Items));
        Assert.Single(tokenRepository.Items);
    }

    [Fact]
    public async Task ShouldReturnEmptyResponseWithoutCreatingUserWhenEmailBelongsToConfirmedUser()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var existing = CreateExistingUser("jdoe", "jdoe@example.com");
        existing.ConfirmEmail();
        userRepository.Items.Add(existing);
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("another", "Another Name", "jdoe@example.com", "Str0ng-Passphrase-1"));

        Assert.Null(response.UserId);
        Assert.Null(response.EmailConfirmationToken);
        Assert.Same(existing, Assert.Single(userRepository.Items));
        Assert.Empty(tokenRepository.Items);
    }

    [Fact]
    public async Task ShouldKeepPendingRegistrationWhenEmailIsReusedWithinConfirmationWindow()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var existing = CreateExistingUser("jdoe", "jdoe@example.com");
        userRepository.Items.Add(existing);
        tokenRepository.Items.Add(CreateConfirmationToken(existing.ExternalId, DateTimeOffset.UtcNow.AddHours(1)));
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("another", "Another Name", "jdoe@example.com", "Str0ng-Passphrase-1"));

        Assert.Null(response.UserId);
        Assert.Null(response.EmailConfirmationToken);
        Assert.Same(existing, Assert.Single(userRepository.Items));
        Assert.Single(tokenRepository.Items);
    }

    [Fact]
    public async Task ShouldReplaceAbandonedRegistrationWhenEmailIsReused()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var abandoned = CreateExistingUser("squatter", "jdoe@example.com");
        userRepository.Items.Add(abandoned);
        tokenRepository.Items.Add(CreateConfirmationToken(abandoned.ExternalId, DateTimeOffset.UtcNow.AddHours(-1)));
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1"));

        var user = Assert.Single(userRepository.Items);
        Assert.Equal("jdoe", user.Login);
        Assert.Equal(user.ExternalId, response.UserId);
        Assert.Equal("raw-token", response.EmailConfirmationToken);
    }

    [Fact]
    public async Task ShouldReplaceAbandonedRegistrationWhenLoginIsReused()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        userRepository.Items.Add(CreateExistingUser("jdoe", "old@example.com"));
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1"));

        var user = Assert.Single(userRepository.Items);
        Assert.Equal("jdoe@example.com", user.Email);
        Assert.Equal(user.ExternalId, response.UserId);
    }

    [Fact]
    public async Task ShouldRemoveBothAbandonedRegistrationsWhenLoginAndEmailBelongToDifferentAccounts()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        userRepository.Items.Add(CreateExistingUser("jdoe", "old@example.com"));
        userRepository.Items.Add(CreateExistingUser("squatter", "jdoe@example.com"));
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1"));

        var user = Assert.Single(userRepository.Items);
        Assert.Equal("jdoe", user.Login);
        Assert.Equal("jdoe@example.com", user.Email);
        Assert.Equal(user.ExternalId, response.UserId);
    }

    [Fact]
    public async Task ShouldRegisterUserAndKeepDeletedAccountWhenEmailBelongsToDeletedUser()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var deleted = CreateExistingUser("old-login", "jdoe@example.com");
        deleted.ConfirmEmail();
        deleted.Delete();
        userRepository.Items.Add(deleted);
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1"));

        Assert.Equal(2, userRepository.Items.Count);
        Assert.Contains(deleted, userRepository.Items);
        var user = userRepository.Items.Single(item => item.DeletedAt is null);
        Assert.Equal("jdoe@example.com", user.Email);
        Assert.Equal(user.ExternalId, response.UserId);
        Assert.Equal("raw-token", response.EmailConfirmationToken);
    }

    [Fact]
    public async Task ShouldThrowDuplicateLoginExceptionWhenLoginBelongsToDeletedUser()
    {
        var userRepository = new FakeUserRepository();
        var tokenRepository = new FakeTokenRepository();
        var deleted = CreateExistingUser("jdoe", "old@example.com");
        deleted.ConfirmEmail();
        deleted.Delete();
        userRepository.Items.Add(deleted);
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        var exception = await Assert.ThrowsAsync<DuplicateLoginException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1")));

        Assert.Equal("Login already in use", exception.Message);
        Assert.Same(deleted, Assert.Single(userRepository.Items));
        Assert.Empty(tokenRepository.Items);
    }

    [Fact]
    public async Task ShouldCommitOnceWhenUserIsRegistered()
    {
        var unitOfWork = new FakeUnitOfWork();
        var interactor = new RegisterUserInteractor(new FakeUserRepository(), new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator(), unitOfWork, new FakeBreachedPasswordChecker());

        await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1"));

        Assert.Equal(1, unitOfWork.Commits);
        Assert.Equal(0, unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldReturnGenericResponseWithoutTokenWhenRepositoryReportsDuplicateEmail()
    {
        var userRepository = new FakeUserRepository { AddException = new DuplicateEmailException() };
        var tokenRepository = new FakeTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), unitOfWork, new FakeBreachedPasswordChecker());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1"));

        Assert.Null(response.UserId);
        Assert.Null(response.EmailConfirmationToken);
        Assert.Empty(tokenRepository.Items);
        Assert.Equal(0, unitOfWork.Commits);
        Assert.Equal(1, unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldReturnSameResponseForDuplicateEmailRaceAndOccupiedEmail()
    {
        var occupied = new FakeUserRepository();
        var existing = CreateExistingUser("owner", "jdoe@example.com");
        existing.ConfirmEmail();
        occupied.Items.Add(existing);
        var occupiedInteractor = new RegisterUserInteractor(occupied, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());
        var raceInteractor = new RegisterUserInteractor(
            new FakeUserRepository { AddException = new DuplicateEmailException() },
            new FakePasswordHasher(),
            new FakeTokenRepository(),
            new FakeTokenGenerator(),
            new FakeUnitOfWork(), new FakeBreachedPasswordChecker());
        var request = new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1");

        var occupiedResponse = await occupiedInteractor.ExecuteAsync(request);
        var raceResponse = await raceInteractor.ExecuteAsync(request);

        Assert.Equal(occupiedResponse, raceResponse);
    }

    [Fact]
    public async Task ShouldThrowLoginAlreadyInUseWhenRepositoryReportsDuplicateLogin()
    {
        var userRepository = new FakeUserRepository { AddException = new DuplicateLoginException() };
        var tokenRepository = new FakeTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), unitOfWork, new FakeBreachedPasswordChecker());

        var exception = await Assert.ThrowsAsync<DuplicateLoginException>(() =>
            interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1")));

        Assert.Equal("Login already in use", exception.Message);
        Assert.Empty(tokenRepository.Items);
        Assert.Equal(1, unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldRollBackAndPropagateWhenTokenInsertFails()
    {
        var tokenRepository = new FakeTokenRepository { AddException = new InvalidOperationException("forced failure") };
        var unitOfWork = new FakeUnitOfWork();
        var interactor = new RegisterUserInteractor(new FakeUserRepository(), new FakePasswordHasher(), tokenRepository, new FakeTokenGenerator(), unitOfWork, new FakeBreachedPasswordChecker());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1")));

        Assert.Equal(0, unitOfWork.Commits);
        Assert.Equal(1, unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task ShouldPropagateOtherRepositoryErrorsUnchanged()
    {
        var failure = new InvalidOperationException("other constraint");
        var userRepository = new FakeUserRepository { AddException = failure };
        var interactor = new RegisterUserInteractor(userRepository, new FakePasswordHasher(), new FakeTokenRepository(), new FakeTokenGenerator(), new FakeUnitOfWork(), new FakeBreachedPasswordChecker());

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Str0ng-Passphrase-1")));

        Assert.Same(failure, thrown);
    }

    private static RegisterUserInteractor CreateInteractor(
        FakeUserRepository repository,
        FakeBreachedPasswordChecker? breachedPasswordChecker = null)
    {
        return new RegisterUserInteractor(
            repository,
            new FakePasswordHasher(),
            new FakeTokenRepository(),
            new FakeTokenGenerator(),
            new FakeUnitOfWork(),
            breachedPasswordChecker ?? new FakeBreachedPasswordChecker());
    }

    private static Task<RegisterUserResponse> Register(
        RegisterUserInteractor interactor,
        string password)
    {
        return interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sh0rt-Pass-14c")]
    [InlineData("fourteen-chars")]
    public async Task ShouldThrowDomainExceptionWhenPasswordIsShorterThanFifteenCharacters(string password)
    {
        var repository = new FakeUserRepository();
        var interactor = CreateInteractor(repository);

        var exception = await Assert.ThrowsAsync<DomainException>(() => Register(interactor, password));

        Assert.Equal("Password must be at least 15 characters long", exception.Message);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldAcceptPasswordWithExactlyFifteenCharacters()
    {
        var repository = new FakeUserRepository();
        var interactor = CreateInteractor(repository);

        await Register(interactor, "kqzvwxpmtbnrhjd");

        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task ShouldAcceptPasswordWithExactlyOneHundredAndTwentyEightCharacters()
    {
        var repository = new FakeUserRepository();
        var interactor = CreateInteractor(repository);

        await Register(interactor, new string('a', 64) + new string('b', 64));

        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenPasswordIsLongerThanOneHundredAndTwentyEightCharacters()
    {
        var repository = new FakeUserRepository();
        var interactor = CreateInteractor(repository);

        var exception = await Assert.ThrowsAsync<DomainException>(() => Register(interactor, new string('a', 129)));

        Assert.Equal("Password must be at most 128 characters long", exception.Message);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldCountCodePointsNotUtf16UnitsWhenCheckingLength()
    {
        var repository = new FakeUserRepository();
        var interactor = CreateInteractor(repository);

        // 15 emojis = 15 code points (30 unidades UTF-16).
        await Register(interactor, string.Concat(Enumerable.Repeat("\U0001F600", 15)));

        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task ShouldAcceptPassphraseWithSpacesAndAccentsAndNoComposition()
    {
        var repository = new FakeUserRepository();
        var interactor = CreateInteractor(repository);

        await Register(interactor, "cavalo bateria grampo çedilha");

        Assert.Single(repository.Items);
    }

    [Theory]
    [InlineData("1q2w3e4r5t6y7u8i")]
    [InlineData("1Q2W3E4R5T6Y7U8I")]
    public async Task ShouldThrowDomainExceptionWhenPasswordIsInTheLocalCommonList(string password)
    {
        var repository = new FakeUserRepository();
        var interactor = CreateInteractor(repository);

        var exception = await Assert.ThrowsAsync<DomainException>(() => Register(interactor, password));

        Assert.Equal("Password is too common or has appeared in a data breach", exception.Message);
        Assert.Empty(repository.Items);
    }

    [Theory]
    [InlineData("my-JDOE-passphrase-x")]
    [InlineData("a-long-JDoe.Example-phrase")]
    [InlineData("the Ouroboros service key")]
    public async Task ShouldThrowDomainExceptionWhenPasswordContainsLoginEmailOrServiceName(string password)
    {
        var repository = new FakeUserRepository();
        var interactor = CreateInteractor(repository);

        var request = new RegisterUserRequest("jdoe", "John Doe", "jdoe.example@example.com", password);

        // "jdoe" tem 4 caracteres: entra na regra. O e-mail contribui com a parte local ("jdoe.example").
        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(request));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldIgnoreLoginShorterThanFourCharactersWhenCheckingContextWords()
    {
        var repository = new FakeUserRepository();
        var interactor = CreateInteractor(repository);

        await interactor.ExecuteAsync(new RegisterUserRequest("joe", "Joe", "joe@example.com", "my-joe-passphrase-x1"));

        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenPasswordHasAppearedInADataBreach()
    {
        var repository = new FakeUserRepository();
        var checker = new FakeBreachedPasswordChecker();
        checker.BreachedPasswords.Add("Str0ng-Passphrase-1");
        var interactor = CreateInteractor(repository, checker);

        var exception = await Assert.ThrowsAsync<DomainException>(() => Register(interactor, "Str0ng-Passphrase-1"));

        Assert.Equal("Password is too common or has appeared in a data breach", exception.Message);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldNotQueryBreachServiceWhenPasswordFailsTheLocalPolicy()
    {
        var checker = new FakeBreachedPasswordChecker();
        var interactor = CreateInteractor(new FakeUserRepository(), checker);

        await Assert.ThrowsAsync<DomainException>(() => Register(interactor, "short"));

        Assert.Empty(checker.Checked);
    }
}
