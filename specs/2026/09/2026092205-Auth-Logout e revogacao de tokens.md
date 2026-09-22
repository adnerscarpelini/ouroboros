# 2026092205 - Logout e revogacao de tokens

**Data:** 22/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

Fechando o fluxo de autenticação, o usuário quer um método de logout, e que o refresh token possa ser revogado tanto por logout quanto por reautenticação (novo login) quanto por expiração natural.

## Análise

Extensão do `auth-service`, depende das specs [2026092203-Auth-Login com JWT e Refresh Token](2026092203-Auth-Login%20com%20JWT%20e%20Refresh%20Token.md) e [2026092204-Auth-Refresh de token com rotacao](2026092204-Auth-Refresh%20de%20token%20com%20rotacao.md). Como o JWT é stateless e de vida curta, ele não é revogado individualmente — a revogação real acontece sobre o refresh token, que é persistido. Expiração já é coberta passivamente pelo campo `expires_at` (checado no login/refresh, sem necessidade de job de limpeza nesta spec). Revogação por reautenticação exige um ajuste no `LoginUseCase` da spec 2026092203 pra revogar os refresh tokens ativos anteriores do usuário antes de emitir um novo.

## Tarefas

- [ ] **Dev** — Criar use case `LogoutUseCase`/`LogoutInteractor`: recebe o refresh token corrente e marca como revogado
- [ ] **Dev** — Criar endpoint `POST /auth/logout` em `AuthController`
- [ ] **Dev** — Ajustar `LoginUseCase` (spec 2026092203) pra revogar os refresh tokens ativos anteriores do usuário ao emitir um novo login
- [ ] **Tester** — Cobrir: logout revoga o token corrente, refresh após logout é rejeitado, novo login revoga tokens anteriores do mesmo usuário
- [ ] **Tech Writer** — Documentar o fluxo de logout e as três formas de revogação (logout, reautenticação, expiração) em `docs/`
