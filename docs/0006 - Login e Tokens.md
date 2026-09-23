# Login e Tokens

## Fluxo

1. O cliente envia login e senha para `POST /api/auth/login`.
2. O `auth-service` busca o usuário pelo login e confere a senha com o hash PBKDF2 gravado.
3. O login só é aceito para usuário **ativo**, ou seja, com o cadastro já confirmado por e-mail (ver `docs/0004 - Confirmacao de Cadastro.md`).
4. A resposta traz dois tokens:
   - **Access token** (JWT): de vida curta, stateless, vai em toda requisição autenticada.
   - **Refresh token**: opaco e de vida longa. É persistido para poder ser consultado e revogado depois.

## Endpoint

```
POST /api/auth/login
{ "login": "jdoe", "password": "S3cret!1" }

200 OK
{
  "tokenType": "Bearer",
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "accessTokenExpiresAt": "2026-09-23T19:14:15+00:00",
  "refreshToken": "gPg99_N_oJWO5Okjmg...",
  "refreshTokenExpiresAt": "2026-09-30T18:59:15+00:00"
}
```

Uso do access token:

```
Authorization: Bearer <accessToken>
```

## Erros

| Situação | Status | Mensagem |
|---|---|---|
| Login inexistente, senha errada ou campos vazios | `401` | `Invalid login or password` |
| Senha correta, mas usuário inativo | `400` | `User is not active` |

Login inexistente e senha errada devolvem **a mesma resposta** de propósito, para não revelar quais logins existem. O usuário inativo só é informado depois que a senha confere.

Todo login rejeitado é logado em `Warning` no Seq, com o login tentado e o motivo.

## Access token (JWT)

- Algoritmo `HS256`, assinado com `Jwt:SigningKey`.
- Validade: `Jwt:AccessTokenExpirationMinutes` (padrão 15 min).
- Claims:

| Claim | Conteúdo |
|---|---|
| `sub` | `id` público do usuário (external id) |
| `unique_name` | login |
| `email` | e-mail |
| `jti` | id único do token |
| `iss` / `aud` | `Jwt:Issuer` / `Jwt:Audience` |
| `iat` / `nbf` / `exp` | emissão / início da validade / expiração |

Os parâmetros estão em `docs/0005 - Configuracao JWT.md`.

## Refresh token

- 32 bytes aleatórios em base64url, gerados pelo mesmo `Sha256TokenGenerator` da confirmação de cadastro.
- Validade: `Jwt:RefreshTokenExpirationDays` (padrão 7 dias).
- Gravado em `auth.refresh_tokens`, **só o hash SHA-256**. O valor em texto puro existe apenas na resposta do login.
- `revoked_at` fica `null` no login. A revogação vem com as specs de refresh (rotação) e logout.

## Onde está no código

- Caso de uso: `Auth.Application/UseCases/Login/`.
- Endpoint: `Auth.Api/Controllers/AuthController.cs`.
- Emissão do JWT: `Auth.Infrastructure/Security/JwtTokenGenerator.cs`.
- Conferência de senha: `Pbkdf2PasswordHasher.Verify`, com comparação em tempo constante.
- Migration: `V20260923160000__CreateRefreshTokensTable.sql`.
