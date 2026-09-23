# 2026092203 - Login com JWT e Refresh Token

**Data:** 22/09/2026
**Status:** Concluido
**Servico(s):** auth-service

## Solicitação

Com o usuário podendo ficar ativo (spec [2026092201](2026092201-Auth-Confirmacao%20de%20cadastro%20por%20email.md)), falta o método de autenticação em si. O usuário pediu o padrão de mercado: login responde com um JWT (access token) + um Refresh Token.

## Análise

Extensão do `auth-service`. Login só é permitido para usuário `Active = true` (reforça a dependência da spec de confirmação de cadastro). O JWT é stateless e de vida curta (parametrizado pela spec [2026092202](2026092202-Auth-Parametros%20de%20configuracao%20de%20autenticacao.md)); o refresh token é opaco, de vida longa, e precisa ser persistido pra poder ser consultado e revogado depois (specs de refresh e logout). Introduz nova tabela `auth.refresh_tokens`.

## Tarefas

- [x] **Dev** — Criar entidade `RefreshToken` em `Auth.Domain` e gateway `IRefreshTokenRepository` em `Auth.Application`
- [x] **Dev** — Criar serviço/gateway de emissão de JWT (ex.: `IJwtTokenGenerator`), implementado em `Auth.Infrastructure` usando `JwtSettings`
- [x] **Dev** — Criar use case `LoginUseCase`/`LoginInteractor`: valida login/senha, valida `Active = true`, emite JWT + refresh token, persiste o refresh token
- [x] **Dev** — Criar endpoint `POST /auth/login` em novo `AuthController`
- [x] **DBA** — Migration criando `auth.refresh_tokens` (id interno, external id, auditoria, `user_id`, `token_hash`, `expires_at`, `revoked_at`)
- [x] **Tester** — Cobrir: login válido emite os dois tokens, usuário inativo é rejeitado, senha incorreta é rejeitada, login inexistente é rejeitado
- [x] **Tech Writer** — Documentar o fluxo de login e o formato da resposta (access token + refresh token) em `docs/`
