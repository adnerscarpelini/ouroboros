namespace Ouroboros.Auth.Application.UseCases.GetUser;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class GetUserInteractorTests
{
    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Items { get; } = new();

        public int LookupCount { get; private set; }

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
            LookupCount++;
            var user = Items.FirstOrDefault(item => item.ExternalId == externalId);
            return Task.FromResult(user);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            LookupCount++;
            var user = Items.FirstOrDefault(item => item.Email == email);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginAsync(string login)
        {
            LookupCount++;
            var user = Items.FirstOrDefault(item => item.Login == login);
            return Task.FromResult(user);
        }

        public Task<User?> GetByLoginOrEmailAsync(string loginOrEmail)
        {
            LookupCount++;
            var user = Items.FirstOrDefault(item => item.Login == loginOrEmail || item.Email == loginOrEmail);
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
            string? email = null,
            string? role = null)
        {
            return new GetUserRequest(
                requester.ExternalId,
                requester.Login,
                requester.Email,
                role ?? requester.Role.ToString(),
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
                role);
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
    public async Task ShouldDenyWithoutQueryingRepositoryWhenUserSearchesAnotherUserByExternalId()
    {
        var scenario = new Scenario();

        await Assert.ThrowsAsync<AccessDeniedException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, externalId: scenario.Other.ExternalId)));
        Assert.Equal(0, scenario.UserRepository.LookupCount);
    }

    [Fact]
    public async Task ShouldDenyWithoutQueryingRepositoryWhenUserSearchesAnotherUserByLogin()
    {
        var scenario = new Scenario();

        await Assert.ThrowsAsync<AccessDeniedException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, login: "other")));
        Assert.Equal(0, scenario.UserRepository.LookupCount);
    }

    [Fact]
    public async Task ShouldDenyWithoutQueryingRepositoryWhenUserSearchesAnotherUserByEmail()
    {
        var scenario = new Scenario();

        await Assert.ThrowsAsync<AccessDeniedException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, email: "other@example.com")));
        Assert.Equal(0, scenario.UserRepository.LookupCount);
    }

    [Fact]
    public async Task ShouldDenyWithoutQueryingRepositoryWhenUserSearchesNonexistentUser()
    {
        var scenario = new Scenario();

        await Assert.ThrowsAsync<AccessDeniedException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, externalId: Guid.NewGuid())));
        Assert.Equal(0, scenario.UserRepository.LookupCount);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("2")]
    [InlineData("")]
    public async Task ShouldDenyWhenRequesterRoleIsNotExactlyAdmin(string role)
    {
        var scenario = new Scenario();

        await Assert.ThrowsAsync<AccessDeniedException>(() => scenario.Interactor.ExecuteAsync(scenario.RequestAs(scenario.Requester, externalId: scenario.Other.ExternalId, role: role)));
        Assert.Equal(0, scenario.UserRepository.LookupCount);
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
