# 2026092204 - Refresh de token com rotacao

**Data:** 22/09/2026
**Servico(s):** auth-service

## Solicitação

O usuário quer poder renovar a sessão sem exigir login novamente, trocando um refresh token válido por um novo par de tokens (JWT + refresh token), como parte do fluxo padrão de mercado de JWT + Refresh Token.

## Análise

Extensão do `auth-service`, depende diretamente da spec 2026092203 (reaproveita `RefreshToken`, `IRefreshTokenRepository` e o gerador de JWT). Segue rotação: cada uso de um refresh token o invalida e emite um novo — um refresh token não pode ser reutilizado depois de trocado. Um refresh token reutilizado (já revogado) deve ser tratado como indício de comprometimento; para esta spec, tratamento mínimo é rejeitar a troca — revogação em cadeia de toda a "família" de tokens fica como possível evolução futura, fora de escopo aqui.
