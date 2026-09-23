# Login e Tokens

## Visão geral

- **Access token** (JWT): de vida curta, stateless, vai em toda requisição autenticada.
- **Refresh token**: opaco e de vida longa. É persistido para poder ser consultado e revogado.
- O login emite o primeiro par de tokens. Depois, o refresh troca o refresh token por um par novo, sem pedir a senha de novo.

Uso do access token:

```
Authorization: Bearer <accessToken>
```

## Login

1. O cliente envia login e senha para `POST /api/auth/login`.
2. O `auth-service` busca o usuário pelo login e confere a senha com o hash PBKDF2 gravado.
3. O login só é aceito para usuário **ativo**, ou seja, com o cadastro já confirmado por e-mail (ver `docs/auth/0001 - Confirmacao de Cadastro.md`).
4. A resposta traz o par de tokens.

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

Erros:

| Situação | Status | Mensagem |
|---|---|---|
| Login inexistente, senha errada ou campos vazios | `401` | `Invalid login or password` |
| Senha correta, mas usuário inativo | `400` | `User is not active` |

Login inexistente e senha errada devolvem **a mesma resposta** de propósito, para não revelar quais logins existem. O usuário inativo só é informado depois que a senha confere.

## Refresh

1. Quando o access token expira, o cliente envia o refresh token para `POST /api/auth/refresh`.
2. O `auth-service` busca o token pelo hash e confere que ele existe, não expirou e não foi revogado.
3. O usuário dono do token precisa continuar ativo.
4. O token recebido é **revogado** e um par novo é emitido. A resposta tem o mesmo formato da do login.

```
POST /api/auth/refresh
{ "refreshToken": "gPg99_N_oJWO5Okjmg..." }

200 OK
{ "tokenType": "Bearer", "accessToken": "...", "accessTokenExpiresAt": "...", "refreshToken": "<novo>", "refreshTokenExpiresAt": "..." }
```

Erros:

| Situação | Status | Mensagem |
|---|---|---|
| Token vazio | `401` | `Refresh token is required` |
| Token não existe | `401` | `Invalid refresh token` |
| Token expirado | `401` | `Refresh token has expired` |
| Token já usado/revogado | `401` | `Refresh token has been revoked` |
| Usuário não está mais ativo | `400` | `User is not active` |

No caso de usuário inativo, o token **não** é consumido.

### Política de rotação

- Cada refresh token vale **uma vez só**. Depois de trocado, o antigo fica revogado (`revoked_at` preenchido).
- O cliente precisa guardar sempre o refresh token **mais recente** da resposta.
- Cada troca gera um token novo com validade cheia (`Jwt:RefreshTokenExpirationDays`, contada a partir do refresh). Enquanto o cliente fizer refresh dentro do prazo, a sessão continua.
- Reusar um token já trocado é rejeitado. Isso indica que o token vazou ou que o cliente tem um bug.
- **Requisições simultâneas** com o mesmo token: só uma recebe o par novo, as outras recebem `401`. A revogação é um `UPDATE ... WHERE revoked_at IS NULL`, então só uma requisição consegue revogar.
- **Fora de escopo por enquanto:** o reuso só é rejeitado. Os outros tokens emitidos a partir do mesmo login (a mesma "família") continuam valendo. Revogar a família inteira no reuso é uma evolução futura possível.
- Revogar o token antigo e gravar o novo são duas escritas, sem transação. Se a segunda falhar, o cliente precisa fazer login de novo, mas nenhum token fica reutilizável.

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

Os parâmetros estão em `docs/auth/0002 - Configuracao JWT.md`.

## Refresh token

- 32 bytes aleatórios em base64url, gerados pelo mesmo `Sha256TokenGenerator` da confirmação de cadastro.
- Validade: `Jwt:RefreshTokenExpirationDays` (padrão 7 dias).
- Gravado em `auth.refresh_tokens`, **só o hash SHA-256**. O valor em texto puro existe apenas na resposta do login/refresh.
- `revoked_at` é `null` enquanto o token está ativo e é preenchido quando ele é trocado no refresh.

## Logs

Todo login ou refresh rejeitado é logado em `Warning` no Seq, com o motivo. No caso do login, também vai o login tentado.

## Onde está no código

- Casos de uso: `Auth.Application/UseCases/Login/` e `Auth.Application/UseCases/RefreshAccessToken/`.
- Endpoints: `Auth.Api/Controllers/AuthController.cs`.
- Regra de revogação: `RefreshToken.Revoke` em `Auth.Domain/Entities/`.
- Revogação concorrente: `DapperRefreshTokenRepository.TryRevokeAsync`.
- Emissão do JWT: `Auth.Infrastructure/Security/JwtTokenGenerator.cs`.
- Conferência de senha: `Pbkdf2PasswordHasher.Verify`, com comparação em tempo constante.
- Migration: `V20260923160000__CreateRefreshTokensTable.sql`.
