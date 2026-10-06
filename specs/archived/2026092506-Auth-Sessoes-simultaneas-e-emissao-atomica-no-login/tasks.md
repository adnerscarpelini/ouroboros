# 2026092506 - Sessoes simultaneas e emissao atomica no login — Tarefas

- [x] **Dev** — Remover a revogação de sessões anteriores do `LoginInteractor`; adicionar `SessionId` à entidade `RefreshToken`; incluir o claim `sid` no `JwtTokenGenerator`
- [x] **Dev** — Criar `User.RegisterLogin(now)` e executar dentro do `IUnitOfWork` a gravação do refresh token, a atualização do usuário (`last_login_at` e contador zerado), devolvendo os tokens só depois do commit
- [x] **Dev** — Criar o caso de uso `LogoutAll` e o endpoint `POST /api/auth/logout-all` (`[Authorize]`, `204`); atualizar a collection Postman
- [x] **DBA** — Migration: `session_id uniqueidentifier` em `auth.refresh_tokens` com backfill, `NOT NULL` e índice `(user_id, session_id)`; persistir e ler `session_id` no `DapperRefreshTokenRepository`
- [x] **Tester** — Unitários: login não revoga sessões anteriores; `LastLoginAt` gravado; contador de falhas zerado; token traz `sid`; logout-all revoga todas
- [x] **Tester** — Integração: dois logins → duas sessões válidas; falha forçada na atualização do usuário → nenhum refresh token criado; logout de uma sessão não afeta a outra
- [x] **Tech Writer** — Atualizar `docs/auth/0003 - Login e Tokens.md`: trocar "uma sessão ativa por usuário" por várias sessões, `sid`, logout-all e garantia transacional
