# Login e Tokens

## Visão geral

- **Access token** (JWT): de vida curta, stateless, vai em toda requisição autenticada.
- **Refresh token**: opaco e de vida longa. É persistido para poder ser consultado e revogado.
- O login emite o primeiro par de tokens. Depois, o refresh troca o refresh token por um par novo, sem pedir a senha de novo.
- O logout revoga o refresh token e encerra a sessão.
- **Uma sessão ativa por usuário:** um novo login revoga as sessões anteriores (ver [Revogação](#revogação)).

Uso do access token:

```
Authorization: Bearer <accessToken>
```

## Login

1. O cliente envia login e senha para `POST /api/auth/login`.
2. O `auth-service` busca o usuário pelo login e confere a senha com o hash PBKDF2 gravado.
3. O login só é aceito para usuário **ativo**, ou seja, com o cadastro já confirmado por e-mail (ver `docs/auth/0001 - Confirmacao de Cadastro.md`).
4. Todos os refresh tokens ainda ativos do usuário são revogados. Um login em outro dispositivo derruba a sessão anterior.
5. A resposta traz o par de tokens novo.

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

Login rejeitado **não** revoga as sessões existentes. Só um login bem-sucedido faz isso.

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

## Logout

1. O cliente envia o refresh token atual para `POST /api/auth/logout`.
2. Se o token estiver ativo, ele é revogado.
3. O cliente descarta os dois tokens.

```
POST /api/auth/logout
{ "refreshToken": "gPg99_N_oJWO5Okjmg..." }

204 No Content
```

- **Idempotente:** logout com um token já revogado ou expirado também devolve `204` e não faz nada, porque não há sessão para encerrar.
- Token vazio → `401 Refresh token is required`. Token que não existe → `401 Invalid refresh token`.
- Não exige o access token: quem tem o refresh token pode encerrar a sessão dele.

## Revogação

Revogar só se aplica ao **refresh token**, que é persistido. O access token (JWT) é stateless e **não é revogado individualmente**. Depois de um logout, ele continua válido até expirar (no máximo `Jwt:AccessTokenExpirationMinutes`, padrão 15 min). Por isso o access token tem vida curta.

Um refresh token deixa de valer de quatro formas:

| Forma | Quando | Efeito |
|---|---|---|
| **Logout** | `POST /api/auth/logout` | Revoga o token enviado (`revoked_at` preenchido). |
| **Reautenticação** | Login bem-sucedido do mesmo usuário | Revoga, num único `UPDATE`, todos os tokens ativos daquele usuário antes de emitir o novo. |
| **Redefinição de senha** | `POST /api/users/password-reset/confirm` concluído | Revoga todos os tokens ativos do usuário, igual à reautenticação (ver `docs/auth/0004 - Recuperacao de Senha.md`). |
| **Expiração** | `expires_at` passou | Nada é gravado: o refresh rejeita com `Refresh token has expired`. Não existe job de limpeza. |

Além delas, o **refresh** revoga o token trocado (ver [Política de rotação](#política-de-rotação)).

Um token é considerado **ativo** quando `revoked_at IS NULL` e `expires_at` ainda não passou.

## Access token (JWT)

- Algoritmo `HS256`, assinado com `Jwt:SigningKey`.
- Validade: `Jwt:AccessTokenExpirationMinutes` (padrão 15 min).
- Claims:

| Claim | Conteúdo |
|---|---|
| `sub` | `id` público do usuário (external id) |
| `unique_name` | login |
| `email` | e-mail |
| `role` | perfil do usuário (`User` ou `Admin`, ver `docs/auth/0005 - Perfis de Acesso.md`) |
| `jti` | id único do token |
| `iss` / `aud` | `Jwt:Issuer` / `Jwt:Audience` |
| `iat` / `nbf` / `exp` | emissão / início da validade / expiração |

Os parâmetros estão em `docs/auth/0002 - Configuracao JWT.md`.

### Validação nas rotas protegidas

Rotas com `[Authorize]` exigem o header `Authorization: Bearer <accessToken>`. A API valida o token com o mesmo `JwtSettings` usado na emissão:

- assinatura com `Jwt:SigningKey`, aceitando **só** `HS256` (bloqueia troca de algoritmo, ex.: `none`);
- `iss` igual a `Jwt:Issuer` e `aud` igual a `Jwt:Audience`;
- `exp` obrigatório, com tolerância de relógio de **30 segundos** (o padrão do ASP.NET é 5 min, longo demais para um token de 15 min);
- claims com os nomes curtos do JWT (`sub`, `role`...), sem mapear para os URIs do `ClaimTypes`. O perfil é lido do claim `role`.

Token ausente, adulterado, expirado ou de outro emissor → `401`, sem corpo.

## Refresh token

- 32 bytes aleatórios em base64url, gerados pelo mesmo `Sha256TokenGenerator` da confirmação de cadastro.
- Validade: `Jwt:RefreshTokenExpirationDays` (padrão 7 dias).
- Gravado em `auth.refresh_tokens`, **só o hash SHA-256**. O valor em texto puro existe apenas na resposta do login/refresh.
- `revoked_at` é `null` enquanto o token está ativo e é preenchido quando ele é revogado (refresh, logout, reautenticação ou redefinição de senha).

## Logs

Todo login, refresh ou logout rejeitado é logado em `Warning` no Seq, com o motivo. No caso do login, também vai o login tentado. Logout com token já revogado/expirado vira um log `Information`.

## Onde está no código

- Casos de uso: `Auth.Application/UseCases/Login/`, `Auth.Application/UseCases/RefreshAccessToken/` e `Auth.Application/UseCases/Logout/`.
- Endpoints: `Auth.Api/Controllers/AuthController.cs`.
- Regra de revogação: `RefreshToken.Revoke` e `RefreshToken.IsActive` em `Auth.Domain/Entities/`.
- Revogação concorrente: `DapperRefreshTokenRepository.TryRevokeAsync`.
- Revogação em lote no login: `DapperRefreshTokenRepository.RevokeAllActiveByUserAsync`.
- Emissão do JWT: `Auth.Infrastructure/Security/JwtTokenGenerator.cs`.
- Validação do JWT: `Auth.Api/Configuration/AuthenticationConfiguration.cs`.
- Conferência de senha: `Pbkdf2PasswordHasher.Verify`, com comparação em tempo constante.
- Migration: `V20260923160000__CreateRefreshTokensTable.sql`.
