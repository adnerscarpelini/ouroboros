namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Linha de base (spec 2026092512): cadastro pela API contra o banco real.
[Collection(AuthApiCollection.Name)]
public sealed class RegistrationApiTests : IAsyncLifetime
{
    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public RegistrationApiTests(AuthApiFixture fixture)
    {
        _fixture = fixture;
        _api = new TestApi(fixture);
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ShouldCreateInactiveUserAndConfirmationTokenWhenDataIsValid()
    {
        var response = await Register("new.user", "new.user@example.com");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var userId = await _api.GetExternalIdAsync("new.user");
        var parameters = new { Id = userId };

        Assert.False(await _api.QueryAsync<bool>("SELECT active FROM auth.users WHERE external_id = @Id;", parameters));
        Assert.False(await _api.QueryAsync<bool>("SELECT email_confirmed FROM auth.users WHERE external_id = @Id;", parameters));
        Assert.Equal("User", await _api.QueryAsync<string>("SELECT role FROM auth.users WHERE external_id = @Id;", parameters));
        Assert.Equal(1, await _api.CountTokensAsync(userId, TokenType.EmailConfirmation));
    }

    [Fact]
    public async Task ShouldNotStorePasswordInPlainText()
    {
        await Register("hashed.user", "hashed.user@example.com");

        var stored = await _api.QueryAsync<string>("SELECT password_hash FROM auth.users WHERE login = 'hashed.user';");

        Assert.NotNull(stored);
        Assert.DoesNotContain(TestApi.Password, stored);
    }

    [Fact]
    public async Task ShouldReturnSameBodyWhenEmailIsAlreadyInUse()
    {
        await _api.CreateUserAsync("owner", "owner@example.com");

        var fresh = await Register("fresh.user", "fresh@example.com");
        var taken = await Register("other.user", "owner@example.com");

        Assert.Equal(HttpStatusCode.Accepted, taken.StatusCode);
        Assert.Equal(await fresh.Content.ReadAsStringAsync(), await taken.Content.ReadAsStringAsync());
        Assert.Equal(2, await _api.CountUsersAsync());
    }

    [Fact]
    public async Task ShouldReturnBadRequestWhenLoginIsAlreadyInUse()
    {
        await _api.CreateUserAsync("taken.login", "first@example.com");

        var response = await Register("taken.login", "second@example.com");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, await _api.CountUsersAsync());
    }

    [Fact]
    public async Task ShouldReturnBadRequestWhenPasswordIsWeak()
    {
        var response = await _api.PostAsync(
            "/api/users",
            new { login = "weak.user", fullName = "Weak", email = "weak@example.com", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _api.CountUsersAsync());
    }

    [Fact]
    public async Task ShouldReturnBadRequestWhenEmailIsInvalid()
    {
        var response = await Register("bad.email", "not-an-email");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _api.CountUsersAsync());
    }

    [Fact]
    public async Task ShouldRollBackUserWhenConfirmationTokenInsertFails()
    {
        using var factory = _fixture.CreateFactoryFailingOn<ITokenRepository>(nameof(ITokenRepository.AddAsync));
        using var api = new TestApi(_fixture, factory.CreateClient());

        var response = await api.PostAsync(
            "/api/users",
            new { login = "rolled.back", fullName = "Rolled Back", email = "rolled.back@example.com", password = TestApi.Password });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, await _api.CountUsersAsync());
    }

    [Fact]
    public async Task ShouldRollBackUserWhenFailureHappensAfterTokenInsert()
    {
        using var factory = _fixture.CreateFactoryFailingOn<ITokenRepository>(nameof(ITokenRepository.AddAsync), FaultTiming.After);
        using var api = new TestApi(_fixture, factory.CreateClient());

        var response = await api.PostAsync(
            "/api/users",
            new { login = "rolled.after", fullName = "Rolled After", email = "rolled.after@example.com", password = TestApi.Password });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, await _api.CountUsersAsync());
        Assert.Equal(0, await _api.QueryAsync<int>("SELECT COUNT(*) FROM auth.tokens;"));
    }

    private Task<HttpResponseMessage> Register(string login, string email) =>
        _api.PostAsync("/api/users", new { login, fullName = "Integration Test", email, password = TestApi.Password });
}
