# 2026092302 - Redefinicao de senha com token

**Data:** 23/09/2026
**Status:** Concluido
**Servico(s):** auth-service

## Solicitação

Segunda etapa da recuperação de senha dos users: um endpoint que confirma a troca de senha, recebendo o token de troca de senha e a nova senha. Devem ser feitas as validações de segurança da senha e, se tudo der certo, aí sim a senha do usuário é trocada. O usuário decidiu também validar se a nova senha é igual à atual, e pediu para seguir os padrões de mercado de segurança no processo.

## Análise

Extensão do `auth-service`, depende da spec [2026092301-Auth-Solicitacao de recuperacao de senha](2026092301-Auth-Solicitacao%20de%20recuperacao%20de%20senha.md) (que gera o token `PasswordReset`). Sem migration: `users` já tem `password_hash` e `password_changed_at`, e `tokens` já tem `used_at`.

A validação de força de senha hoje é privada no `RegisterUserInteractor`; precisa ser extraída para uma política reutilizável, para que cadastro e redefinição apliquem exatamente a mesma regra.

Decisões de segurança (OWASP Forgot Password Cheat Sheet), aprovadas pelo usuário:

- Token validado por hash, restrito ao tipo `PasswordReset` (um token de confirmação de e-mail não serve aqui), não expirado e não usado — **uso único**.
- Mensagem de erro genérica para token inexistente, de outro tipo, expirado ou já usado, sem revelar qual caso ocorreu.
- **Nova senha não pode ser igual à atual** (verificada via `IPasswordHasher.Verify` contra o hash atual).
- Nova senha passa pela mesma política de força do cadastro.
- Após a troca: `PasswordChangedAt` atualizado, token marcado como usado e **todos os refresh tokens ativos do usuário revogados** (encerra sessões de um eventual invasor).
- **Sem login automático** após a redefinição — o usuário autentica de novo com a senha nova.
- Usuário inativo não pode redefinir (mesma regra da solicitação).
- Rate limiting no endpoint, contra força bruta de tokens.
- Notificação por e-mail de "sua senha foi alterada" fica para quando existir o serviço de e-mail.

## Tarefas

- [x] **Dev** — Extrair a validação de força de senha do `RegisterUserInteractor` para uma política reutilizável e fazer o cadastro usá-la
- [x] **Dev** — Criar `User.ChangePassword(string passwordHash)` em `Auth.Domain`, atualizando `PasswordHash` e `PasswordChangedAt`
- [x] **Dev** — Criar use case `ResetPasswordUseCase`/`ResetPasswordInteractor`: valida o token (`PasswordReset`, existe, não expirado, não usado), exige usuário ativo, valida força da nova senha e que ela é diferente da atual, troca a senha, marca o token como usado e revoga todos os refresh tokens ativos do usuário (`RevokeAllActiveByUserAsync`)
- [x] **Dev** — Criar endpoint `POST /api/users/password-reset/confirm` em `UserController`, recebendo `{ token, newPassword }`, com erro genérico para token inválido e rate limiting
- [x] **Tester** — Cobrir: caminho feliz (senha trocada, `PasswordChangedAt` atualizado, token usado, refresh tokens revogados), token inexistente, expirado, já usado, de outro tipo (`EmailConfirmation`), usuário inativo, senha fraca, senha igual à atual, e ausência de regressão da política de senha no cadastro
- [x] **Tech Writer** — Documentar o fluxo completo de recuperação de senha (solicitação + redefinição) em `docs/auth/`, com as decisões de segurança e o aviso de que o envio por e-mail ainda não existe
