namespace Ouroboros.Auth.Integration.Tests.Api;

using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Ouroboros.Auth.Domain.Entities;
using Ouroboros.Auth.Integration.Tests.Infrastructure;
using Xunit;

// Linha de base (spec 2026092512): middleware JWT, autorizacao por perfil e consulta de usuario contra o banco real.
[Collection(AuthApiCollection.Name)]
public sealed class UserQueryApiTests : IAsyncLifetime
{
    private readonly AuthApiFixture _fixture;
    private readonly TestApi _api;

    public UserQueryApiTests(AuthApiFixture fixture)
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
    public async Task ShouldReturnUnauthorizedWhenThereIsNoToken()
    {
        var user = await _api.CreateUserAsync("query.anonymous");

        var byId = await _api.GetAsync($"/api/users/{user.ExternalId}");
        var search = await _api.PostAsync("/api/users/search", new { login = "query.anonymous" });

        Assert.Equal(HttpStatusCode.Unauthorized, byId.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, search.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnUnauthorizedWhenTokenIsSignedWithAnotherKey()
    {
        var user = await _api.CreateUserAsync("query.forged");
        var forged = CreateJwt(user, "another-signing-key-with-32-bytes-or-more!!", DateTime.UtcNow.AddMinutes(10));

        var response = await _api.GetAsync($"/api/users/{user.ExternalId}", forged);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnUnauthorizedWhenTokenIsExpired()
    {
        var user = await _api.CreateUserAsync("query.expired");
        var expired = CreateJwt(user, AuthApiFactory.SigningKey, DateTime.UtcNow.AddMinutes(-10));

        var response = await _api.GetAsync($"/api/users/{user.ExternalId}", expired);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnUnauthorizedWhenTokenUsesAlgorithmNone()
    {
        var user = await _api.CreateUserAsync("query.none");
        var header = Base64Url("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64Url($$"""{"sub":"{{user.ExternalId}}","iss":"ouroboros-auth","aud":"ouroboros","exp":{{DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()}}}""");

        var response = await _api.GetAsync($"/api/users/{user.ExternalId}", $"{header}.{payload}.");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnOnlyAllowedFieldsWhenUserQueriesItself()
    {
        var user = await _api.CreateUserAsync("query.self", "query.self@example.com");
        var session = await _api.LoginAsync("query.self");

        var response = await _api.GetAsync($"/api/users/{user.ExternalId}", session.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = await TestApi.ReadJsonAsync(response);
        var properties = body.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray();

        Assert.Equal(["active", "createdAt", "email", "externalId", "fullName", "login", "role"], properties);
        Assert.Equal("query.self", body.RootElement.GetProperty("login").GetString());
        Assert.Equal("User", body.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task ShouldFindSelfBySearchByLoginAndEmail()
    {
        await _api.CreateUserAsync("query.search", "Query.Search@example.com");
        var session = await _api.LoginAsync("query.search");

        var byLogin = await _api.PostAsync("/api/users/search", new { login = "QUERY.SEARCH" }, session.AccessToken);
        var byEmail = await _api.PostAsync("/api/users/search", new { email = "query.search@EXAMPLE.com" }, session.AccessToken);

        Assert.Equal(HttpStatusCode.OK, byLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byEmail.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnForbiddenWhenCommonUserQueriesAnotherUser()
    {
        await _api.CreateUserAsync("query.common");
        var other = await _api.CreateUserAsync("query.other");
        var session = await _api.LoginAsync("query.common");

        var byId = await _api.GetAsync($"/api/users/{other.ExternalId}", session.AccessToken);
        var byLogin = await _api.PostAsync("/api/users/search", new { login = "query.other" }, session.AccessToken);
        var unknown = await _api.GetAsync($"/api/users/{Guid.NewGuid()}", session.AccessToken);

        // Negar sem consultar o banco: a conta inexistente responde igual a uma existente de outro dono.
        Assert.Equal(HttpStatusCode.Forbidden, byId.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unknown.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnAnotherUserWhenRequesterIsAdmin()
    {
        var admin = await _api.CreateUserAsync("query.admin");
        await _api.SetRoleAsync(admin.ExternalId, UserRole.Admin);
        var other = await _api.CreateUserAsync("query.target");
        var session = await _api.LoginAsync("query.admin");

        var response = await _api.GetAsync($"/api/users/{other.ExternalId}", session.AccessToken);
        var unknown = await _api.GetAsync($"/api/users/{Guid.NewGuid()}", session.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnBadRequestWhenSearchHasMoreThanOneCriterion()
    {
        await _api.CreateUserAsync("query.many");
        var session = await _api.LoginAsync("query.many");

        var response = await _api.PostAsync(
            "/api/users/search",
            new { login = "query.many", email = "query.many@example.com" },
            session.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ShouldNotReturnDeletedUserToAdmin()
    {
        var admin = await _api.CreateUserAsync("query.admin.deleted");
        await _api.SetRoleAsync(admin.ExternalId, UserRole.Admin);
        var gone = await _api.CreateUserAsync("query.gone");
        await _api.ExecuteAsync("UPDATE auth.users SET deleted_at = SYSDATETIMEOFFSET(), active = 0 WHERE external_id = @Id;", new { Id = gone.ExternalId });
        var session = await _api.LoginAsync("query.admin.deleted");

        var response = await _api.GetAsync($"/api/users/{gone.ExternalId}", session.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Spec 2026092510: o privilegio vem do banco, nao do token.

    [Fact]
    public async Task ShouldDenyDemotedAdminImmediatelyEvenWithTheOldTokenStillValid()
    {
        var admin = await _api.CreateUserAsync("demoted.admin");
        await _api.SetRoleAsync(admin.ExternalId, UserRole.Admin);
        var other = await _api.CreateUserAsync("demoted.target");
        var session = await _api.LoginAsync("demoted.admin");

        var before = await _api.GetAsync($"/api/users/{other.ExternalId}", session.AccessToken);

        await _api.SetRoleAsync(admin.ExternalId, UserRole.User);

        var byId = await _api.GetAsync($"/api/users/{other.ExternalId}", session.AccessToken);
        var bySearch = await _api.PostAsync("/api/users/search", new { login = "demoted.target" }, session.AccessToken);
        var self = await _api.GetAsync($"/api/users/{admin.ExternalId}", session.AccessToken);

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byId.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, bySearch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, self.StatusCode);
    }

    [Fact]
    public async Task ShouldAllowPromotedUserImmediatelyEvenWithTheOldTokenStillValid()
    {
        var user = await _api.CreateUserAsync("promoted.user");
        var other = await _api.CreateUserAsync("promoted.target");
        var session = await _api.LoginAsync("promoted.user");

        var before = await _api.GetAsync($"/api/users/{other.ExternalId}", session.AccessToken);

        await _api.SetRoleAsync(user.ExternalId, UserRole.Admin);

        var after = await _api.GetAsync($"/api/users/{other.ExternalId}", session.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task ShouldReturnUnauthorizedWithGenericBodyWhenRequesterWasDeletedAndTokenIsStillValid()
    {
        var user = await _api.CreateUserAsync("deleted.requester");
        var session = await _api.LoginAsync("deleted.requester");
        await _api.ExecuteAsync("UPDATE auth.users SET deleted_at = SYSDATETIMEOFFSET(), active = 0 WHERE external_id = @Id;", new { Id = user.ExternalId });

        var byId = await _api.GetAsync($"/api/users/{user.ExternalId}", session.AccessToken);
        var bySearch = await _api.PostAsync("/api/users/search", new { login = "deleted.requester" }, session.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, byId.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, bySearch.StatusCode);
        Assert.Equal("Invalid access token", await TestApi.ReadErrorAsync(byId));
        Assert.Equal("Invalid access token", await TestApi.ReadErrorAsync(bySearch));
    }

    private static string CreateJwt(User user, string signingKey, DateTime expires)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "ouroboros-auth",
            Audience = "ouroboros",
            Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = user.ExternalId.ToString() },
            NotBefore = expires.AddMinutes(-20),
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static string Base64Url(string value) => Base64UrlEncoder.Encode(value);
}
