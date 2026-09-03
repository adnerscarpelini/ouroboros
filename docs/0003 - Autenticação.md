# 0003 - Autenticação

Fluxo completo de autenticação do serviço Auth: cadastro, confirmação de e-mail, login, refresh token, logout e redefinição de senha. Diagrama correspondente em [docs/excalidraw/0003 - Autenticação.excalidraw](excalidraw/0003%20-%20Autenticação.excalidraw) (ainda não reflete refresh token/logout).

## Visão geral

1. Usuário se cadastra (`POST /api/auth/register`) — a conta nasce inativa.
2. A Api enfileira um e-mail de confirmação e gera um token de validação.
3. Usuário confirma o e-mail (link do e-mail ou `POST /api/auth/confirm-email`) — a conta vira ativa.
4. Usuário faz login (`POST /api/auth/login`) e recebe um par de tokens: um JWT (`AccessToken`) e um refresh token.
5. Quando o `AccessToken` expira, o cliente troca o refresh token por um par novo (`POST /api/auth/refresh-token`), sem precisar logar de novo.
6. Ao encerrar a sessão, o cliente revoga o refresh token corrente (`POST /api/auth/logout`).
7. Se esquecer a senha, usuário solicita redefinição (`POST /api/auth/forgot-password`) e confirma com o token recebido por e-mail (`POST /api/auth/reset-password`).
8. Todo endpoint da Api exige o `AccessToken` por padrão, exceto os marcados com `[AllowAnonymous]`.

Endpoints `[AllowAnonymous]`: `register`, `confirm-email` (`GET`/`POST`), `login`, `refresh-token`, `forgot-password` e `reset-password` — é o próprio fluxo de autenticação, não faria sentido exigir token pra eles. `logout` é a exceção: exige `AccessToken` válido, porque só faz sentido chamado por quem já está autenticado.

## 1. Cadastro — `POST /api/auth/register`

Request (`RegisterUserRequest`): `Login`, `FullName`, `Email`, `Password`.

- `Email` validado como e-mail (`[EmailAddress]`).
- `Password` validado por `StrongPasswordAttribute` (senha forte).

`UserService.CreateUserAsync`:

1. Rejeita se `Login` já está em uso (`409 Conflict`).
2. Rejeita se `Email` já está em uso (`409 Conflict`).
3. Gera hash da senha (Argon2, `IPasswordHasher`).
4. Cria o `User` com `EmailConfirmed = false` e `IsActive = false` — conta ainda não pode logar.
5. Enfileira o e-mail de confirmação (ver seção seguinte).
6. Retorna `201 Created` com o `ExternalId` do usuário (`RegisterUserResponse`).

### Enfileiramento do e-mail de confirmação

Dentro da mesma operação de cadastro, `EnqueueValidationEmailAsync`:

1. Gera um token aleatório (`ITokenGenerator.GenerateToken`) e guarda só o hash dele (`TokenGenerator.Hash`) — o token bruto nunca é persistido.
2. Monta a URL de confirmação: `{ApiBaseUrl}/api/auth/confirm-email?token={token}`.
3. Renderiza o template `UserCreationValidationEmail.html` com nome do usuário e link.
4. Enfileira o e-mail via `IEmailQueueService.EnqueueAsync` — cria um `EmailMessage` (schema `common`) com `Sent = false`. **Não há hoje um worker que efetivamente envia esse e-mail** — o envio real ainda não está implementado, só o enfileiramento.
5. Cria um `Token` (schema `auth`, tipo `UserCreationValidation`) associado ao usuário e ao `EmailMessage`, com validade de **24 horas**.

## 2. Confirmação de e-mail

Dois endpoints chamam a mesma regra (`UserService.ConfirmEmailAsync`):

- `GET /api/auth/confirm-email?token=...` — o link clicável dentro do e-mail. Retorna uma página HTML (`ConfirmationSuccess.html` ou `ConfirmationFailure.html`), não JSON.
- `POST /api/auth/confirm-email` (`ConfirmEmailRequest`) — pensado pra clientes de API (app, frontend próprio). Retorna `204 No Content` em caso de sucesso ou `400 Bad Request`.

`ConfirmEmailAsync`:

1. Faz hash do token recebido e busca o `Token` correspondente pelo hash.
2. Falha (`"Token inválido."`) se não encontrar.
3. Falha (`"Token já foi utilizado."`) se já validado — token é de uso único.
4. Falha (`"Token expirado."`) se passou das 24h.
5. Falha (`"Token inválido."`) se o tipo do token não for `UserCreationValidation`.
6. Marca o token como validado (`Token.Validate()`).
7. Marca o usuário como confirmado e ativo (`User.ConfirmEmail()` → `EmailConfirmed = true`, `IsActive = true`).

## 3. Login — `POST /api/auth/login`

Request (`LoginRequest`): `Login`, `Password`.

`UserService.LoginAsync`:

1. Busca o usuário por `Login`. Se não existir, falha com mensagem genérica **"Login ou senha inválidos."** — não revela se o problema foi o login ou a senha.
2. Se a conta está bloqueada (`User.IsLockedOut()`), falha com **"Conta temporariamente bloqueada por excesso de tentativas."**.
3. Verifica a senha (Argon2). Se errada:
   - Registra a tentativa falha (`RegisterFailedLoginAttempt()`).
   - Na 5ª tentativa falha consecutiva, bloqueia a conta por **15 minutos** e zera o contador.
   - Falha com a mesma mensagem genérica do passo 1.
4. Se a senha está correta mas o e-mail não foi confirmado (`IsActive == false`), falha com **"Confirme seu e-mail antes de fazer login."**.
5. Login bem-sucedido: zera tentativas falhas, remove bloqueio, atualiza `LastLoginAt` (`RegisterSuccessfulLogin()`).
6. Emite o par de tokens (`IssueAuthenticationResult`, ver seção 4) e retorna `AccessToken` + `ExpiresAt` + `RefreshToken` + `RefreshTokenExpiresAt` (`LoginResponse`, `200 OK`).

### Bloqueio por tentativas (regras de `User`, domínio)

- Máximo de **5 tentativas falhas** antes de bloquear.
- Bloqueio dura **15 minutos**, contados a partir da tentativa que estourou o limite.
- Ao bloquear, o contador de tentativas é zerado — o bloqueio em si é que impede novas tentativas até expirar, não o contador.

### Token JWT (access token)

- Assinado com **RS256** (par de chaves RSA assimétrico, não uma chave simétrica compartilhada): a chave privada (`Jwt:SigningKeyPem`, User Secrets) só existe no Auth e nunca sai daqui; a chave pública (`Jwt:PublicKeyPem`, User Secrets) valida o token e é o que qualquer outro serviço vai usar pra aceitar sessões emitidas pelo Auth, sem precisar confiar num segredo compartilhado. Ver [docs/0002](0002%20-%20Setup%20do%20Banco%20de%20Dados%20Local.md) pra gerar o par e [docs/0000](0000%20-%20Arquitetura.md#autenticação-entre-serviços) pra decisão completa.
- Validade: **1 hora**.
- Claims: `sub` (ExternalId do usuário), `unique_name` (Login), `email`.
- Expirado, o cliente troca o refresh token por um par novo (seção 4) em vez de logar de novo.

## 4. Refresh token e logout

Sessão representada por uma `RefreshToken` (schema `auth`, entidade própria — não reaproveita `Token`/`TokenType`, que são acoplados ao fluxo de e-mail via `EmailMessageId`). Guarda só o hash do token (`ITokenGenerator.Hash`), igual aos demais tokens do serviço — o valor bruto nunca é persistido.

### Emissão — `UserService.IssueAuthenticationResult`

Chamado tanto pelo login quanto pelo refresh:

1. Gera o `AccessToken` (JWT, `IJwtTokenGenerator.GenerateToken`).
2. Gera um refresh token aleatório e persiste um `RefreshToken` com o hash dele, validade de **30 dias** (`RevokedAt = null`).
3. Retorna os dois pares (`AccessToken`/`ExpiresAt` e `RefreshToken`/`RefreshTokenExpiresAt`) num único `AuthenticationResult`.

### Renovação — `POST /api/auth/refresh-token` (`RefreshTokenRequest`: `RefreshToken`)

`UserService.RefreshTokenAsync`, com **rotação**: cada uso do refresh token o revoga e emite um par novo — um token roubado só funciona até a próxima renovação legítima.

1. Faz hash do token recebido e busca o `RefreshToken` correspondente pelo hash.
2. Falha (`"Token inválido."`) se não encontrar ou se já estiver revogado (`RevokedAt` setado).
3. Falha (`"Token expirado."`) se passou dos 30 dias.
4. Revoga o token usado (`RefreshToken.Revoke()`) e emite um par novo (`IssueAuthenticationResult`).
5. Retorna `200 OK` (`LoginResponse`) ou `401 Unauthorized` em caso de falha.

### Logout — `POST /api/auth/logout` (`LogoutRequest`: `RefreshToken`)

`UserService.LogoutAsync`: revoga o refresh token recebido (`RefreshToken.Revoke()`), impedindo renovações futuras com ele. Não precisa cruzar com o usuário do `AccessToken` — a posse do refresh token bruto já prova o direito de encerrar aquela sessão, mesmo modelo de confiança usado em `ConfirmEmailAsync`/`ResetPasswordAsync`.

1. Falha (`"Token inválido."`) se não encontrar o token pelo hash, ou se já estiver revogado.
2. Revoga e retorna `204 No Content`, ou `400 Bad Request` em caso de falha.

## 5. Redefinição de senha

Dois endpoints:

- `POST /api/auth/forgot-password` (`ForgotPasswordRequest`: `Email`) — solicita a redefinição. Sempre retorna `204 No Content`, exista ou não o e-mail — não revela se a conta existe (evita enumeração de contas).
- `POST /api/auth/reset-password` (`ResetPasswordRequest`: `Token`, `NewPassword`) — confirma a redefinição com o token recebido por e-mail. Retorna `204 No Content` em caso de sucesso ou `400 Bad Request`.

### Solicitação — `UserService.RequestPasswordResetAsync`

1. Busca o usuário pelo `Email`. Se não encontrar, não faz mais nada (resposta continua `204`).
2. Se encontrar, invalida qualquer token de redefinição pendente e ainda não usado desse usuário (`Token.Validate()` reaproveitado como invalidação — impede que um link antigo continue valendo depois de um pedido mais novo).
3. Gera um token aleatório e guarda só o hash dele, igual ao fluxo de confirmação de e-mail.
4. Monta a URL de redefinição: `{ApiBaseUrl}/reset-password?token={token}` — hoje aponta pra um caminho sem página própria (não há front-end no projeto ainda); quando o front-end existir, é ele quem coleta a nova senha e chama `POST /api/auth/reset-password`.
5. Renderiza o template `PasswordResetEmail.html` e enfileira o e-mail (`IEmailQueueService`, mesmo aviso do fluxo de confirmação: só enfileira, não envia de fato ainda).
6. Cria um `Token` (schema `auth`, tipo `PasswordReset`) associado ao usuário e ao `EmailMessage`, com validade de **1 hora** (mais curta que as 24h da confirmação de e-mail, por ser mais sensível).

### Confirmação — `UserService.ResetPasswordAsync`

1. Faz hash do token recebido e busca o `Token` correspondente pelo hash.
2. Falha (`"Token inválido."`) se não encontrar, ou se o tipo do token não for `PasswordReset`.
3. Falha (`"Token já foi utilizado."`) se já validado — token é de uso único.
4. Falha (`"Token expirado."`) se passou de 1 hora.
5. Gera o hash da nova senha (Argon2) e chama `User.ResetPassword(newPasswordHash)`: atualiza `PasswordHash` e `PasswordChangedAt`, e zera `FailedLoginAttempts`/`LockedUntil` — a senha nova invalida o motivo de um bloqueio antigo.
6. Marca o token como validado (`Token.Validate()`).

## 6. Autorização por padrão

Configurado em `Program.cs` via `FallbackPolicy`: todo endpoint da Api exige usuário autenticado (JWT válido) por padrão. Só os endpoints marcados explicitamente com `[AllowAnonymous]` — listados na seção "Visão geral" — ficam abertos; `logout` continua exigindo autenticação. Ver seção "Autorização de endpoints" da skill [ags-developer](../.claude/skills/ags-developer/SKILL.md).
