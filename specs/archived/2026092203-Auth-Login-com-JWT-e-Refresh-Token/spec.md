# 2026092203 - Login com JWT e Refresh Token

**Data:** 22/09/2026
**Servico(s):** auth-service

## Solicitação

Com o usuário podendo ficar ativo (spec 2026092201), falta o método de autenticação em si. O usuário pediu o padrão de mercado: login responde com um JWT (access token) + um Refresh Token.

## Análise

Extensão do `auth-service`. Login só é permitido para usuário `Active = true` (reforça a dependência da spec de confirmação de cadastro). O JWT é stateless e de vida curta (parametrizado pela spec 2026092202); o refresh token é opaco, de vida longa, e precisa ser persistido pra poder ser consultado e revogado depois (specs de refresh e logout). Introduz nova tabela `auth.refresh_tokens`.
