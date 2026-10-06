namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Ouroboros.Auth.Integration.Tests.Security;
using Xunit;

// Spec 2026092509: politica de senha, senhas vazadas e re-hash, pela API.
[Collection(AuthApiCollection.Name)]
public sealed class PasswordPolicyApiTests : IAsyncLifetime
{
    private const string Passphrase = "cavalo bateria grampo cedilha";

    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public PasswordPolicyApiTests(AuthApiFixture fixture)
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
    public async Task ShouldAcceptPassphraseWithoutCompositionRules()
    {
        var response = await Register("phrase.user", Passphrase);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, await _api.CountUsersAsync());
    }

    [Theory]
    [InlineData("Ab1!Ab1!Ab1!Ab")]
    [InlineData("1q2w3e4r5t6y7u8i")]
    public async Task ShouldRejectShortOrCommonPasswordWhenRegistering(string password)
    {
        var response = await Register("weak.policy", password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _api.CountUsersAsync());
    }

    [Fact]
    public async Task ShouldRejectPasswordLongerThanOneHundredAndTwentyEightCharacters()
    {
        var response = await Register("long.policy", new string('a', 129));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _api.CountUsersAsync());
    }

    [Fact]
    public async Task ShouldRejectPasswordContainingTheLogin()
    {
        var response = await Register("contextual.user", "my contextual.user secret");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await _api.CountUsersAsync());
    }

    [Fact]
    public async Task ShouldRejectBreachedPasswordWithTheSingleMessage()
    {
        _fixture.Factory.BreachedPasswordChecker.MarkAsBreached("leaked passphrase from a breach");

        var response = await Register("breached.user", "leaked passphrase from a breach");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Password is too common or has appeared in a data breach", await TestApi.ReadErrorAsync(response));
        Assert.Equal(0, await _api.CountUsersAsync());
    }

    [Fact]
    public async Task ShouldKeepOldPasswordWorkingAndRehashItOnTheNextSuccessfulLogin()
    {
        var user = await _api.CreateUserAsync("old.cost");
        var oldHash = PasswordHasherTests.HashWithIterations(TestApi.Password, 100_000);
        await _api.ExecuteAsync("UPDATE auth.users SET password_hash = @Hash WHERE external_id = @Id;", new { Hash = oldHash, Id = user.ExternalId });

        var wrong = await _api.PostAsync("/api/auth/login", new { login = "old.cost", password = "Wrong-Password-123" });
        var afterWrong = await StoredHashAsync(user.ExternalId);
        var login = await _api.PostAsync("/api/auth/login", new { login = "old.cost", password = TestApi.Password });
        var afterLogin = await StoredHashAsync(user.ExternalId);
        var again = await _api.PostAsync("/api/auth/login", new { login = "old.cost", password = TestApi.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(oldHash, afterWrong);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.StartsWith("600000.", afterLogin);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    private Task<string> StoredHashAsync(Guid externalId) =>
        _api.QueryAsync<string>("SELECT password_hash FROM auth.users WHERE external_id = @Id;", new { Id = externalId })!;

    private Task<HttpResponseMessage> Register(string login, string password) =>
        _api.PostAsync("/api/users", new { login, fullName = "Policy Test", email = $"{login}@example.com", password });
}
