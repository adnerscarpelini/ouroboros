# 2026092515 - Limpeza de tokens expirados

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar a limpeza periódica de tokens expirados para evitar crescimento ilimitado das tabelas.

## Análise

`auth.tokens` e `auth.refresh_tokens` guardam linhas expiradas indefinidamente. Criar rotina idempotente, em lotes, com observabilidade e período de retenção explícito. A limpeza não pode alterar a validade lógica de tokens ativos nem impedir investigação de eventos recentes. Se uma futura detecção de reuso exigir histórico da família de refresh tokens, sua retenção deverá ser compatível com o prazo dessa detecção. O job pertence ao `auth-service` ou à sua operação, sem novo bounded context.

## Tarefas

- [ ] **Dev** — Agendar a rotina de limpeza e registrar métricas/contagens, sem incluir valores de tokens nos logs.
- [ ] **DBA** — Criar exclusão em lotes com índices e política de retenção adequados, evitando bloqueios longos no PostgreSQL.
- [ ] **Tester** — Cobrir preservação de tokens ativos, retenção de histórico recente, idempotência e execução com muitos registros.
- [ ] **Tech Writer** — Documentar retenção, agendamento e operação da limpeza em `docs/auth/`.
