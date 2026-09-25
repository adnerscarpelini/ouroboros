# 2026092507 - Rotacao atomica de refresh token

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar a troca consistente de um refresh token por outro, inclusive sob concorrência.

## Análise

O `TryRevokeAsync` garante que somente uma requisição revogue o token antigo, mas a inserção do sucessor ocorre depois, sem transação. Uma falha perde a sessão; a condição SQL também precisa conferir que o token ainda não expirou. Preservar a rotação da spec 2026092204. Detecção e revogação de família por reuso ficam para evolução posterior; por ora o reuso continua rejeitado. O endpoint permanece público porque o refresh token é a credencial, que nunca deve aparecer em log ou URL.

## Tarefas

- [ ] **Dev** — Coordenar consumo do token antigo e criação do sucessor numa operação indivisível; emitir o par somente após commit.
- [ ] **DBA** — Implementar revogação condicional que verifica prazo no banco e inserir o novo token na mesma transação.
- [ ] **Tester** — Cobrir refresh simultâneo, expiração durante a operação, falha na inserção do sucessor e rollback usando PostgreSQL real.
- [ ] **Tech Writer** — Atualizar `docs/auth/0003 - Login e Tokens.md`, removendo a limitação documentada de duas escritas sem transação.
