# 2026092201 - Confirmacao de cadastro por email — Tarefas

- [x] **Dev** — Criar entidade `Token` (ou `EmailConfirmationToken`) e enum/tipo `TokenType` em `Auth.Domain`, com o tipo `EmailConfirmation` como primeiro valor
- [x] **Dev** — Criar gateway `ITokenRepository` em `Auth.Application`
- [x] **Dev** — Ajustar `RegisterUserInteractor` (ou criar use case dedicado) pra gerar o token de confirmação ao registrar o usuário
- [x] **Dev** — Criar use case `ConfirmEmailUseCase`/`ConfirmEmailInteractor` que valida o token (existe, não expirou, não foi usado) e ativa o usuário (`Active = true`, `EmailConfirmed = true`)
- [x] **Dev** — Criar endpoint de confirmação em `UserController` (ex.: `POST /users/confirm-email`) recebendo o token
- [x] **Dev** — Retornar/logar o token gerado no registro, com nota explícita de que é temporário até existir envio de e-mail
- [x] **DBA** — Migration criando `auth.tokens` (id interno, external id, auditoria, `user_id`, `type`, `token_hash`, `expires_at`, `used_at`)
- [x] **Tester** — Cobrir: geração do token no registro, confirmação com token válido, token expirado, token já usado, token inexistente
- [x] **Tech Writer** — Documentar o fluxo de confirmação de cadastro em `docs/`, deixando explícito que o envio por e-mail ainda não existe
