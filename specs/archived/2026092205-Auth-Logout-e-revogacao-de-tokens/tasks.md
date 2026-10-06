# 2026092205 - Logout e revogacao de tokens — Tarefas

- [x] **Dev** — Criar use case `LogoutUseCase`/`LogoutInteractor`: recebe o refresh token corrente e marca como revogado
- [x] **Dev** — Criar endpoint `POST /auth/logout` em `AuthController`
- [x] **Dev** — Ajustar `LoginUseCase` (spec 2026092203) pra revogar os refresh tokens ativos anteriores do usuário ao emitir um novo login
- [x] **Tester** — Cobrir: logout revoga o token corrente, refresh após logout é rejeitado, novo login revoga tokens anteriores do mesmo usuário
- [x] **Tech Writer** — Documentar o fluxo de logout e as três formas de revogação (logout, reautenticação, expiração) em `docs/`
