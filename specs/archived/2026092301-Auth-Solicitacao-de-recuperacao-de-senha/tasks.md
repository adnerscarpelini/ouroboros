# 2026092301 - Solicitacao de recuperacao de senha — Tarefas

- [x] **Dev** — Adicionar `PasswordReset` ao enum `TokenType` em `Auth.Domain`
- [x] **Dev** — Criar `IUserRepository.GetByLoginOrEmailAsync(string loginOrEmail)` em `Auth.Application`
- [x] **Dev** — Criar `ITokenRepository.InvalidatePendingByUserAsync(Guid userExternalId, TokenType type, DateTimeOffset invalidatedAt)` para invalidar tokens pendentes (não usados e não expirados) do usuário
- [x] **Dev** — Criar use case `RequestPasswordResetUseCase`/`RequestPasswordResetInteractor`: busca o usuário por login ou e-mail; se não existir ou estiver inativo/sem e-mail confirmado, termina sem erro e sem token; caso contrário, invalida os tokens de reset pendentes, gera um novo token de 1h e retorna o token em claro apenas para o controller logar
- [x] **Dev** — Criar endpoint `POST /api/users/password-reset/request` em `UserController`: resposta sempre `202 Accepted` com mensagem genérica, token nunca no corpo, apenas logado com TODO temporário (mesmo padrão do cadastro)
- [x] **Dev** — Configurar rate limiting (middleware nativo do ASP.NET Core) no endpoint de solicitação
- [x] **DBA** — Implementar no Dapper `GetByLoginOrEmailAsync` e `InvalidatePendingByUserAsync` (sem migration — `type` já é `text`)
- [x] **Tester** — Cobrir: solicitação por login, solicitação por e-mail, usuário inexistente (sem erro, sem token), usuário inativo/sem e-mail confirmado (sem erro, sem token), invalidação dos tokens de reset anteriores, token gerado com tipo `PasswordReset` e expiração de 1h
