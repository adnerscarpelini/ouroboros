namespace Ouroboros.Auth.Infrastructure.Security;

using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Domain.Entities;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    // Nome curto de claim usado por OIDC/Keycloak/Entra ID; quem valida o token configura RoleClaimType = "role".
    public const string RoleClaimType = "role";

    // Claim de sessao do OpenID Connect: o servico sabe qual e a sessao atual (ex.: trocar a senha preserva a de quem trocou).
    public const string SessionIdClaimType = "sid";

    private readonly JwtSettings _settings;
    private readonly SigningKeyStore _signingKeys;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public JwtTokenGenerator(
        JwtSettings settings,
        SigningKeyStore signingKeys)
    {
        _settings = settings;
        _signingKeys = signingKeys;
    }

    public AccessToken Generate(
        Guid userId,
        string login,
        string email,
        UserRole role,
        Guid sessionId)
    {
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_settings.AccessTokenExpirationMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,

            // RS256 com o kid da chave Active no header (o handler le o KeyId da chave).
            SigningCredentials = _signingKeys.ActiveCredentials,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, login),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(RoleClaimType, role.ToString()),
                new Claim(SessionIdClaimType, sessionId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
        };

        var token = _tokenHandler.CreateToken(descriptor);

        return new AccessToken(token, new DateTimeOffset(expiresAt));
    }
}
