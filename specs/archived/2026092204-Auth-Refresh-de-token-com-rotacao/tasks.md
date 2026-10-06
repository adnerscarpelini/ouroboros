# 2026092204 - Refresh de token com rotacao — Tarefas

- [x] **Dev** — Criar use case `RefreshTokenUseCase`/`RefreshTokenInteractor`: valida o refresh token recebido (existe, não expirou, não foi revogado), revoga o token usado e emite um novo par (JWT + refresh token)
- [x] **Dev** — Criar endpoint `POST /auth/refresh` em `AuthController`
- [x] **Tester** — Cobrir: rotação válida emite novo par e revoga o antigo, token expirado é rejeitado, token já revogado/reutilizado é rejeitado, token inexistente é rejeitado
- [x] **Tech Writer** — Documentar o fluxo de refresh e a política de rotação em `docs/`
