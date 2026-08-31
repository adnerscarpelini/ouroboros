# 0003 - Autenticação

Fluxo completo de autenticação do módulo Auth: cadastro, confirmação de e-mail e login. Diagrama correspondente em [docs/excalidraw/0003 - Autenticação.excalidraw](excalidraw/0003%20-%20Autenticação.excalidraw).

## Visão geral

1. Usuário se cadastra (`POST /api/auth/register`) — a conta nasce inativa.
2. A Api enfileira um e-mail de confirmação e gera um token de validação.
3. Usuário confirma o e-mail (link do e-mail ou `POST /api/auth/confirm-email`) — a conta vira ativa.
4. Usuário faz login (`POST /api/auth/login`) e recebe um JWT.
5. Todo endpoint da Api exige esse JWT por padrão, exceto os marcados com `[AllowAnonymous]`.

Todos os quatro endpoints abaixo são `[AllowAnonymous]` — é o próprio fluxo de autenticação, não faria sentido exigir token pra eles.

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
6. Gera o JWT (`IJwtTokenGenerator.GenerateToken`) e retorna `AccessToken` + `ExpiresAt` (`LoginResponse`, `200 OK`).

### Bloqueio por tentativas (regras de `User`, domínio)

- Máximo de **5 tentativas falhas** antes de bloquear.
- Bloqueio dura **15 minutos**, contados a partir da tentativa que estourou o limite.
- Ao bloquear, o contador de tentativas é zerado — o bloqueio em si é que impede novas tentativas até expirar, não o contador.

### Token JWT

- Assinado com HMAC SHA256, chave em `Jwt:SigningKey` (User Secrets — ver [docs/0002 - Setup do Banco de Dados Local.md](0002%20-%20Setup%20do%20Banco%20de%20Dados%20Local.md)).
- Validade: **1 hora**.
- Claims: `sub` (ExternalId do usuário), `unique_name` (Login), `email`.
- Não há refresh token — expirado, o usuário precisa logar de novo.

## 4. Autorização por padrão

Configurado em `Program.cs` via `FallbackPolicy`: todo endpoint da Api exige usuário autenticado (JWT válido) por padrão. Só os endpoints marcados explicitamente com `[AllowAnonymous]` — os quatro deste fluxo — ficam abertos. Ver seção "Autorização de endpoints" da skill [ags-developer](../.claude/skills/ags-developer/SKILL.md).
