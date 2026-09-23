# Confirmação de Cadastro

> **O e-mail ainda não é enviado.** Não existe mensageria no projeto. Por enquanto, o token de confirmação volta na resposta do cadastro e aparece no log. Isso é temporário e está marcado com `TODO` no código.

## Fluxo

1. `POST /api/users` cria o usuário **inativo** (`active = false`, `email_confirmed = false`).
2. No mesmo request, é gerado um token de confirmação válido por **24h**. O valor em texto puro volta no campo `emailConfirmationToken` da resposta.
3. O cliente envia o token para `POST /api/users/confirm-email`.
4. Se o token for válido, o usuário passa a `active = true` e `email_confirmed = true`, e o token é marcado como usado.

## Endpoints

Cadastro:

```
POST /api/users
{ "login": "jdoe", "fullName": "John Doe", "email": "jdoe@example.com", "password": "S3cret!1" }

201 Created
{ "id": "...", "login": "jdoe", "fullName": "John Doe", "email": "jdoe@example.com", "emailConfirmationToken": "UO7xZ5mw..." }
```

Confirmação:

```
POST /api/users/confirm-email
{ "token": "UO7xZ5mw..." }

200 OK
{ "userId": "...", "login": "jdoe", "email": "jdoe@example.com" }
```

## Erros da confirmação

Todos devolvem `400 {"error": "..."}`, com o motivo logado em `Warning` no Seq.

| Situação | Mensagem |
|---|---|
| Token vazio | `Confirmation token is required` |
| Token não existe | `Invalid confirmation token` |
| Token já usado | `Token has already been used` |
| Token expirado | `Token has expired` |

Não existe reenvio de token hoje. Se o token expirar, o usuário fica inativo até esse fluxo existir.

## Tabela `auth.tokens`

Tabela genérica, pensada para reaproveitar em outros fluxos de token sem criar uma tabela nova por caso.

- `user_id`: FK para `auth.users`.
- `type`: nome do enum `TokenType` gravado como texto (hoje `EmailConfirmation` e `PasswordReset`, ver `docs/auth/0004 - Recuperacao de Senha.md`). Para um fluxo novo, adicione um valor ao enum.
- `token_hash`: hash SHA-256 do token, com índice único. **O token em texto puro nunca é gravado.** A busca é feita pelo hash.
- `expires_at` / `used_at`: validade e uso único.

## Onde está no código

- Domínio: `Token`, `TokenType` e `User.ConfirmEmail()` em `Auth.Domain/Entities/`.
- Casos de uso: `RegisterUser` (gera o token) e `ConfirmEmail` em `Auth.Application/UseCases/`.
- Geração e hash do token: `Sha256TokenGenerator` em `Auth.Infrastructure/Security/`.
- Migration: `V20260923100000__CreateTokensTable.sql`.

## Quando existir envio de e-mail

- Remover `EmailConfirmationToken` de `RegisterUserResponse`.
- Remover o log do token em `UserController.Register`.
- Enviar o token num link que chama `POST /api/users/confirm-email`.
