namespace Ouroboros.Auth.Application.UseCases.RegisterUser;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Xunit;

public class RegisterUserInteractorTests
{
    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Items { get; } = new();

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
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password)
        {
            return $"hashed:{password}";
        }
    }

    [Fact]
    public async Task ShouldRegisterUserInactiveWhenDataIsValid()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher());

        var response = await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3cret!1"));

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("jdoe", response.Login);
        Assert.Equal("John Doe", response.FullName);
        Assert.Equal("jdoe@example.com", response.Email);
        Assert.Single(repository.Items);
        Assert.Equal("hashed:S3cret!1", repository.Items[0].PasswordHash);
        Assert.False(repository.Items[0].Active);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenLoginIsInvalid()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("   ", "John Doe", "jdoe@example.com", "S3cret!1")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenEmailIsInvalid()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "not-an-email", "S3cret!1")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenLoginOrEmailAlreadyExists()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher());

        await interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3cret!1"));

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "Another Name", "other@example.com", "S3cret!1")));
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenPasswordIsTooShort()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3c!1")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenPasswordHasNoUppercaseLetter()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "s3cret!1")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenPasswordHasNoLowercaseLetter()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "S3CRET!1")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenPasswordHasNoDigit()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Secret!!")));
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhenPasswordHasNoSpecialCharacter()
    {
        var repository = new FakeUserRepository();
        var interactor = new RegisterUserInteractor(repository, new FakePasswordHasher());

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new RegisterUserRequest("jdoe", "John Doe", "jdoe@example.com", "Secret123")));
        Assert.Empty(repository.Items);
    }
}
