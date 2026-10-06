# 2026092205 - Logout e revogacao de tokens

**Data:** 22/09/2026
**Servico(s):** auth-service

## Solicitação

Fechando o fluxo de autenticação, o usuário quer um método de logout, e que o refresh token possa ser revogado tanto por logout quanto por reautenticação (novo login) quanto por expiração natural.

## Análise

Extensão do `auth-service`, depende das specs 2026092203 e 2026092204. Como o JWT é stateless e de vida curta, ele não é revogado individualmente — a revogação real acontece sobre o refresh token, que é persistido. Expiração já é coberta passivamente pelo campo `expires_at` (checado no login/refresh, sem necessidade de job de limpeza nesta spec). Revogação por reautenticação exige um ajuste no `LoginUseCase` da spec 2026092203 pra revogar os refresh tokens ativos anteriores do usuário antes de emitir um novo.
