# 2026092519 - Auditoria de eventos de seguranca — Tarefas

- [ ] **Dev** — Criar `IAuditLog` e o contexto de requisição (IP, user agent) na Application, montado pela Api; registrar cada evento da decisão 2 nos interactors, com sucesso dentro da transação e falha fora dela
- [ ] **DBA** — Migration de `auth.audit_events` com `CHECK` de `event_type` e `outcome`, índices, grants só de `INSERT`/`SELECT` para `auth_service` e a função `auth.purge_audit_events(before)` `SECURITY DEFINER`; criar o `DapperAuditLog`
- [ ] **Dev** — Chamar a purga (retenção de 1 ano, configurável) no job da 2026092515
- [ ] **Tester** — Unitários: cada fluxo registra o evento certo; nenhum evento carrega senha, token ou login digitado de conta inexistente. Integração: um rollback remove o evento de sucesso e mantém o de falha; `auth_service` não consegue `UPDATE` nem `DELETE` na tabela; a purga respeita a retenção
- [ ] **Tech Writer** — Criar doc em `docs/auth/` com o catálogo de eventos, o que nunca é registrado, a retenção, a base LGPD e consultas SQL de exemplo
