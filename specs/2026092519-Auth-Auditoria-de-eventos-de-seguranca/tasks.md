# 2026092519 - Auditoria de eventos de seguranca — Tarefas

- [ ] **Dev** — Criar `IAuditLog` e o contexto de requisição (IP, user agent) na Application, montado pela Api; registrar cada evento da decisão 2 nos interactors, com sucesso dentro da transação e falha fora dela
- [ ] **DBA** — Migration de `auth.audit_events` com `CHECK` de `event_type` e `outcome`, índices e grants só de `INSERT`/`SELECT` para `auth_service`; criar o `DapperAuditLog`
- [ ] **Tester** — Unitários: cada fluxo registra o evento certo; nenhum evento carrega senha, token ou login digitado de conta inexistente. Integração: um rollback remove o evento de sucesso e mantém o de falha; `auth_service` não consegue `UPDATE` nem `DELETE` na tabela
- [ ] **Tech Writer** — Criar doc em `docs/auth/` com o catálogo de eventos, o que nunca é registrado, a retenção, a base LGPD, a retenção como tarefa operacional (com o script de exemplo) e consultas SQL de exemplo
