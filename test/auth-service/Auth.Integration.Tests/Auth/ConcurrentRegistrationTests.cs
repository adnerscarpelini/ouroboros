namespace Ouroboros.Auth.Integration.Tests.Auth;

using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Domain.Exceptions;
using Ouroboros.Auth.Infrastructure.Persistence;
using Ouroboros.Auth.Infrastructure.Security;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Spec 2026092502: cadastro atomico e tratamento da disputa por login ou e-mail contra o SQL Server real.
[Collection(AuthApiCollection.Name)]
public sealed class ConcurrentRegistrationTests : IAsyncLifetime
{
    private const string Password = "Correct-Password-123";
    private const int Rounds = 8;

    private readonly AuthApiFixture _fixture;

    public ConcurrentRegistrationTests(AuthApiFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ShouldTranslateLoginIndexViolationToDuplicateLoginException()
    {
        await AddUserAsync("dup.login", "first@example.com");

        await Assert.ThrowsAsync<DuplicateLoginException>(() => AddUserAsync("DUP.LOGIN", "second@example.com"));
    }

    [Fact]
    public async Task ShouldTranslateEmailIndexViolationToDuplicateEmailException()
    {
        await AddUserAsync("first.user", "dup@example.com");

        await Assert.ThrowsAsync<DuplicateEmailException>(() => AddUserAsync("second.user", "DUP@example.com"));
    }

    [Fact]
    public async Task ShouldReturnOneAcceptedAndOneBadRequestWhenSameLoginIsRegisteredConcurrently()
    {
        using var client = _fixture.CreateClient();

        for (var round = 0; round < Rounds; round++)
        {
            var login = $"race.login.{round}";

            var responses = await Task.WhenAll(
                RegisterAsync(client, login, $"a.{round}@example.com"),
                RegisterAsync(client, login, $"b.{round}@example.com"));

            var statuses = responses.Select(response => response.StatusCode).Order().ToArray();

            Assert.Equal([HttpStatusCode.Accepted, HttpStatusCode.BadRequest], statuses);
            Assert.Equal(1, await CountUsersAsync("login", login));
        }
    }

    [Fact]
    public async Task ShouldReturnAcceptedForBothAndCreateOneAccountWhenSameEmailIsRegisteredConcurrently()
    {
        using var client = _fixture.CreateClient();

        for (var round = 0; round < Rounds; round++)
        {
            var email = $"race.email.{round}@example.com";

            var responses = await Task.WhenAll(
                RegisterAsync(client, $"first.{round}", email),
                RegisterAsync(client, $"second.{round}", email));

            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
            Assert.Equal(1, await CountUsersAsync("email", email));
        }
    }

    [Fact]
    public async Task ShouldKeepAbandonedAccountAndCreateNoUserWhenTokenInsertFails()
    {
        var abandoned = await AddUserAsync("abandoned", "abandoned@example.com");

        using var failingFactory = _fixture.CreateFactoryFailingOn<ITokenRepository>(nameof(ITokenRepository.AddAsync));
        using var client = failingFactory.CreateClient();

        var response = await RegisterAsync(client, "abandoned", "new.owner@example.com");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, await CountUsersAsync("login", "abandoned"));
        Assert.Equal(1, await CountUsersAsync("email", "abandoned@example.com"));
        Assert.Equal(0, await CountUsersAsync("email", "new.owner@example.com"));
        Assert.Equal(abandoned.ExternalId, await GetExternalIdAsync("abandoned"));
    }

    private async Task<User> AddUserAsync(string login, string email)
    {
        var user = User.Create(login, "Integration Test", email, new Pbkdf2PasswordHasher().Hash(Password));

        await using var session = _fixture.CreateSession();
        await new DapperUserRepository(session, NullLogger<DapperUserRepository>.Instance).AddAsync(user);

        return user;
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string login, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { login, fullName = "Integration Test", email, password = Password }),
        };
        request.Headers.Add(AuthApiFactory.RemoteIpHeader, AuthApiFixture.NextIp());

        return client.SendAsync(request);
    }

    private async Task<int> CountUsersAsync(string column, string value)
    {
        var sql = $"SELECT COUNT(*) FROM auth.users WHERE {column} = @Value;";

        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<int>(sql, new { Value = value });
    }

    private async Task<Guid> GetExternalIdAsync(string login)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<Guid>(
            "SELECT external_id FROM auth.users WHERE login = @Login;",
            new { Login = login });
    }
}
