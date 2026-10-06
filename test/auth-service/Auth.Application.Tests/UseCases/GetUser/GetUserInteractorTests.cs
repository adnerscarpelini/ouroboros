namespace Ouroboros.Auth.Application.UseCases.GetUser;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class GetUserInteractorTests
{
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

        public int LookupCount { get; private set; }

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

        public List<Guid> LookedUpExternalIds { get; } = new();

        // Como o repositorio real, ignora conta excluida: pra aplicacao ela nao existe.
        public Task<User?> GetByExternalIdAsync(Guid externalId)
        {
            LookupCount++;
            LookedUpExternalIds.Add(externalId);
            var user = Items.FirstOrDefault(item => item.ExternalId == externalId && item.DeletedAt is null);
            return Task.FromResult(user);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            LookupCount++;
            var user = Items.FirstOrDefault(item => item.NormalizedEmail == email);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginAsync(string login)
        {
            LookupCount++;
            var user = Items.FirstOrDefault(item => item.NormalizedLogin == login);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginOrEmailAsync(string loginOrEmail)
        {
            LookupCount++;
            var user = Items.FirstOrDefault(item => item.NormalizedLogin == loginOrEmail || item.NormalizedEmail == loginOrEmail);
            return Task.FromResult(user);
        }

        public Task UpdateAsync(User user)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class Scenario
    {
        public FakeUserRepository UserRepository { get; } = new();

        public User Admin { get; }

        public User Requester { get; }

        public User Other { get; }

        public GetUserInteractor Interactor { get; }

        public Scenario()
        {
            Admin = CreateUser("admin", "admin@example.com", UserRole.Admin);
            Requester = CreateUser("jdoe", "jdoe@example.com", UserRole.User);
            Other = CreateUser("other", "other@example.com", UserRole.User);
            UserRepository.Items.AddRange([Admin, Requester, Other]);
            Interactor = new GetUserInteractor(UserRepository);
        }

        public GetUserRequest RequestAs(
            User requester,
            Guid? externalId = null,
            string? login = null,
            string? email = null)
        {
            return new GetUserRequest(
                requester.ExternalId,
                externalId,
                login,
                email);
        }

        private static User CreateUser(
            string login,
            string email,
            UserRole role)
        {
            return User.Rehydrate(
                1,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddDays(-30),
                null,
                login,
                "Full Name",
                email,
                true,
                "hashed-password",
                DateTimeOffset.UtcNow.AddDays(-30),
                true,
                DateTimeOffset.UtcNow.AddDays(-1),
                role,
                null);
        }
    }

    [Fact]
    public async Task ShouldReturnUserWhenAdminSearchesByExternalId()
    {
        var scenario = new Scenario();

        var response = await scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Admin, externalId: scenario.Other.ExternalId));

        Assert.Equal(scenario.Other.ExternalId, response.ExternalId);
        Assert.Equal("other", response.Login);
        Assert.Equal("Full Name", response.FullName);
        Assert.Equal("other@example.com", response.Email);
        Assert.Equal("User", response.Role);
        Assert.True(response.Active);
        Assert.Equal(scenario.Other.CreatedAt, response.CreatedAt);
    }

    [Fact]
    public async Task ShouldReturnUserWhenAdminSearchesByLogin()
    {
        var scenario = new Scenario();

        var response = await scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Admin, login: " other "));

        Assert.Equal(scenario.Other.ExternalId, response.ExternalId);
    }

    [Fact]
    public async Task ShouldReturnUserWhenAdminSearchesByEmail()
    {
        var scenario = new Scenario();

        var response = await scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Admin, email: "other@example.com"));

        Assert.Equal(scenario.Other.ExternalId, response.ExternalId);
    }

    [Fact]
    public async Task ShouldReturnOwnDataWhenUserSearchesSelfByExternalId()
    {
        var scenario = new Scenario();

        var response = await scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, externalId: scenario.Requester.ExternalId));

        Assert.Equal(scenario.Requester.ExternalId, response.ExternalId);
    }

    [Fact]
    public async Task ShouldReturnOwnDataWhenUserSearchesSelfByLogin()
    {
        var scenario = new Scenario();

        var response = await scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, login: "jdoe"));

        Assert.Equal(scenario.Requester.ExternalId, response.ExternalId);
    }

    [Fact]
    public async Task ShouldReturnOwnDataWhenUserSearchesSelfByEmail()
    {
        var scenario = new Scenario();

        var response = await scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, email: "jdoe@example.com"));

        Assert.Equal(scenario.Requester.ExternalId, response.ExternalId);
    }

    [Fact]
    public async Task ShouldDenyWithoutQueryingTargetWhenUserSearchesAnotherUserByExternalId()
    {
        var scenario = new Scenario();

        await Assert.ThrowsAsync<AccessDeniedException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, externalId: scenario.Other.ExternalId)));
        AssertOnlyRequesterWasRead(scenario);
    }

    [Fact]
    public async Task ShouldDenyWithoutQueryingTargetWhenUserSearchesAnotherUserByLogin()
    {
        var scenario = new Scenario();

        await Assert.ThrowsAsync<AccessDeniedException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, login: "other")));
        AssertOnlyRequesterWasRead(scenario);
    }

    [Fact]
    public async Task ShouldDenyWithoutQueryingTargetWhenUserSearchesAnotherUserByEmail()
    {
        var scenario = new Scenario();

        await Assert.ThrowsAsync<AccessDeniedException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, email: "other@example.com")));
        AssertOnlyRequesterWasRead(scenario);
    }

    [Fact]
    public async Task ShouldDenyWithoutQueryingTargetWhenUserSearchesNonexistentUser()
    {
        var scenario = new Scenario();

        await Assert.ThrowsAsync<AccessDeniedException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, externalId: Guid.NewGuid())));
        AssertOnlyRequesterWasRead(scenario);
    }

    [Fact]
    public async Task ShouldDecideByTheRoleStoredInTheDatabase()
    {
        // O token nao entra mais na decisao: quem foi rebaixado no banco e tratado como User na hora,
        // e quem foi promovido ja consulta como Admin.
        var scenario = new Scenario();

        await Assert.ThrowsAsync<AccessDeniedException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, externalId: scenario.Other.ExternalId)));
        var response = await scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Admin, externalId: scenario.Other.ExternalId));

        Assert.Equal(scenario.Other.ExternalId, response.ExternalId);
    }

    [Fact]
    public async Task ShouldThrowInvalidAccessTokenExceptionWhenRequesterDoesNotExist()
    {
        var scenario = new Scenario();
        var stranger = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidAccessTokenException>(() => scenario.Interactor.ExecuteAsync(new GetUserRequest(stranger, scenario.Other.ExternalId, null, null)));

        Assert.Equal("Invalid access token", exception.Message);
        Assert.Equal([stranger], scenario.UserRepository.LookedUpExternalIds);
        Assert.Equal(1, scenario.UserRepository.LookupCount);
    }

    [Fact]
    public async Task ShouldThrowInvalidAccessTokenExceptionWhenRequesterWasDeleted()
    {
        var scenario = new Scenario();
        scenario.Admin.Delete();

        await Assert.ThrowsAsync<InvalidAccessTokenException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Admin, externalId: scenario.Other.ExternalId)));

        Assert.Equal(1, scenario.UserRepository.LookupCount);
    }

    [Fact]
    public async Task ShouldCompareSelfWithTheLoginAndEmailStoredInTheDatabase()
    {
        // O login e o e-mail do solicitante vem do banco, normalizados: o token nao pode forjar "ser o proprio usuario".
        var scenario = new Scenario();

        var byLogin = await scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, login: " JDOE "));
        var byEmail = await scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, email: "JDoe@Example.com"));

        Assert.Equal(scenario.Requester.ExternalId, byLogin.ExternalId);
        Assert.Equal(scenario.Requester.ExternalId, byEmail.ExternalId);
        await Assert.ThrowsAsync<AccessDeniedException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, login: "other")));
    }

    [Fact]
    public async Task ShouldNotQueryTargetAgainWhenRequesterSearchesSelf()
    {
        var scenario = new Scenario();

        await scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, login: "jdoe"));

        Assert.Equal([scenario.Requester.ExternalId], scenario.UserRepository.LookedUpExternalIds);
        Assert.Equal(1, scenario.UserRepository.LookupCount);
    }

    [Fact]
    public async Task ShouldThrowUserNotFoundExceptionWhenAdminSearchesNonexistentUser()
    {
        var scenario = new Scenario();

        await Assert.ThrowsAsync<UserNotFoundException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Admin, externalId: Guid.NewGuid())));
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenNoCriterionIsInformed()
    {
        var scenario = new Scenario();

        var exception = await Assert.ThrowsAsync<DomainException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Admin, login: "   ", email: "")));

        Assert.IsType<DomainException>(exception);
        Assert.Equal(0, scenario.UserRepository.LookupCount);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenMoreThanOneCriterionIsInformed()
    {
        var scenario = new Scenario();

        var exception = await Assert.ThrowsAsync<DomainException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Admin, login: "other", email: "other@example.com")));

        Assert.IsType<DomainException>(exception);
        Assert.Equal(0, scenario.UserRepository.LookupCount);
    }

    private static void AssertOnlyRequesterWasRead(Scenario scenario)
    {
        // So o solicitante e lido, pra decidir pelo perfil dele no banco. O alvo nunca e consultado antes de negar.
        Assert.Equal([scenario.Requester.ExternalId], scenario.UserRepository.LookedUpExternalIds);
        Assert.Equal(1, scenario.UserRepository.LookupCount);
    }

    [Fact]
    public void ShouldExposeOnlyAllowedFieldsInResponse()
    {
        var allowedFields = new[] { "Active", "CreatedAt", "Email", "ExternalId", "FullName", "Login", "Role" };

        var responseFields = typeof(GetUserResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Order();

        Assert.Equal(allowedFields, responseFields);
    }
}
