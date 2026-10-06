# 2026092203 - Login com JWT e Refresh Token — Tarefas

- [x] **Dev** — Criar entidade `RefreshToken` em `Auth.Domain` e gateway `IRefreshTokenRepository` em `Auth.Application`
- [x] **Dev** — Criar serviço/gateway de emissão de JWT (ex.: `IJwtTokenGenerator`), implementado em `Auth.Infrastructure` usando `JwtSettings`
- [x] **Dev** — Criar use case `LoginUseCase`/`LoginInteractor`: valida login/senha, valida `Active = true`, emite JWT + refresh token, persiste o refresh token
- [x] **Dev** — Criar endpoint `POST /auth/login` em novo `AuthController`
- [x] **DBA** — Migration criando `auth.refresh_tokens` (id interno, external id, auditoria, `user_id`, `token_hash`, `expires_at`, `revoked_at`)
- [x] **Tester** — Cobrir: login válido emite os dois tokens, usuário inativo é rejeitado, senha incorreta é rejeitada, login inexistente é rejeitado
- [x] **Tech Writer** — Documentar o fluxo de login e o formato da resposta (access token + refresh token) em `docs/`
