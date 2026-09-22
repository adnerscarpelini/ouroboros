# 2026092204 - Refresh de token com rotacao

**Data:** 22/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário quer poder renovar a sessão sem exigir login novamente, trocando um refresh token válido por um novo par de tokens (JWT + refresh token), como parte do fluxo padrão de mercado de JWT + Refresh Token.

## Análise

Extensão do `auth-service`, depende diretamente da spec [2026092203-Auth-Login com JWT e Refresh Token](2026092203-Auth-Login%20com%20JWT%20e%20Refresh%20Token.md) (reaproveita `RefreshToken`, `IRefreshTokenRepository` e o gerador de JWT). Segue rotação: cada uso de um refresh token o invalida e emite um novo — um refresh token não pode ser reutilizado depois de trocado. Um refresh token reutilizado (já revogado) deve ser tratado como indício de comprometimento; para esta spec, tratamento mínimo é rejeitar a troca — revogação em cadeia de toda a "família" de tokens fica como possível evolução futura, fora de escopo aqui.

## Tarefas

- [ ] **Dev** — Criar use case `RefreshTokenUseCase`/`RefreshTokenInteractor`: valida o refresh token recebido (existe, não expirou, não foi revogado), revoga o token usado e emite um novo par (JWT + refresh token)
- [ ] **Dev** — Criar endpoint `POST /auth/refresh` em `AuthController`
- [ ] **Tester** — Cobrir: rotação válida emite novo par e revoga o antigo, token expirado é rejeitado, token já revogado/reutilizado é rejeitado, token inexistente é rejeitado
- [ ] **Tech Writer** — Documentar o fluxo de refresh e a política de rotação em `docs/`
