# 2026092302 - Redefinicao de senha com token — Tarefas

- [x] **Dev** — Extrair a validação de força de senha do `RegisterUserInteractor` para uma política reutilizável e fazer o cadastro usá-la
- [x] **Dev** — Criar `User.ChangePassword(string passwordHash)` em `Auth.Domain`, atualizando `PasswordHash` e `PasswordChangedAt`
- [x] **Dev** — Criar use case `ResetPasswordUseCase`/`ResetPasswordInteractor`: valida o token (`PasswordReset`, existe, não expirado, não usado), exige usuário ativo, valida força da nova senha e que ela é diferente da atual, troca a senha, marca o token como usado e revoga todos os refresh tokens ativos do usuário (`RevokeAllActiveByUserAsync`)
- [x] **Dev** — Criar endpoint `POST /api/users/password-reset/confirm` em `UserController`, recebendo `{ token, newPassword }`, com erro genérico para token inválido e rate limiting
- [x] **Tester** — Cobrir: caminho feliz (senha trocada, `PasswordChangedAt` atualizado, token usado, refresh tokens revogados), token inexistente, expirado, já usado, de outro tipo (`EmailConfirmation`), usuário inativo, senha fraca, senha igual à atual, e ausência de regressão da política de senha no cadastro
- [x] **Tech Writer** — Documentar o fluxo completo de recuperação de senha (solicitação + redefinição) em `docs/auth/`, com as decisões de segurança e o aviso de que o envio por e-mail ainda não existe
