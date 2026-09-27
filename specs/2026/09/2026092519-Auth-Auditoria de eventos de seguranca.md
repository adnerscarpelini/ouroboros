# 2026092519 - Auditoria de eventos de seguranca

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, os eventos de segurança apareceram registrados só no Serilog, sem trilha própria de auditoria. O usuário aprovou criar um registro de auditoria dos eventos de autenticação e de conta.

## Análise

Extensão do `auth-service`. Depende de 2026092516 (unidade de trabalho), 2026092511 (papéis de banco separados) e 2026092515 (job periódico). Deve vir depois das specs que criam os eventos: 2026092506, 2026092507, 2026092517 e 2026092520. Referências: OWASP Logging Cheat Sheet e os logs de eventos de IdPs (sign-in logs do Entra ID, events do Keycloak).

Decisões:
1. **Tabela `auth.audit_events`, só de inserção.** Colunas:

   | Coluna | Conteúdo |
   |---|---|
   | `id`, `external_id` | Identificação do evento |
   | `occurred_at` | Quando aconteceu |
   | `event_type` | Tipo do evento (decisão 2) |
   | `outcome` | `Success` ou `Failure` |
   | `user_external_id` | Conta afetada, **sem FK**, para o evento sobreviver à remoção de cadastro abandonado |
   | `actor_external_id` | Quem agiu, quando não é o próprio usuário (Admin) |
   | `session_id` | Sessão, quando existir |
   | `ip_address` | `inet` |
   | `user_agent` | Truncado em 256 caracteres |
   | `reason` | Código fixo, por exemplo `invalid_password`, `locked_out`, `reuse_detected` |
2. **Eventos registrados:** `UserRegistered`, `EmailConfirmed`, `LoginSucceeded`, `LoginFailed`, `AccountLockedOut`, `RefreshTokenReuseDetected`, `Logout`, `LogoutAll`, `PasswordResetRequested` (só quando a conta existe, e isso nunca é exposto ao cliente), `PasswordResetCompleted`, `PasswordChanged`, `UserDeleted` e `RoleChanged`. O refresh normal **não** é auditado, pelo volume, e já está no log.
3. **Nunca entram na auditoria:** senha, token, hash, nem o login digitado numa tentativa contra conta inexistente. Usuários digitam a senha no campo de login por engano. Nesse caso `user_external_id` fica nulo.
4. **Onde cada evento é gravado:**
   - evento de sucesso, na mesma transação da operação (2026092516). Se a operação foi desfeita, não há evento;
   - evento de falha, numa transação própria, para sobreviver ao rollback.
5. **Gateway `IAuditLog` na Application.** Os interactors registram os eventos. IP e user agent chegam por um contexto de requisição que a Api monta, porque a Application não conhece `HttpContext`.
6. **Só inserção, garantido pelo banco:**
   - o papel `auth_service` recebe apenas `INSERT` e `SELECT` na tabela (2026092511);
   - a purga, com retenção de **1 ano**, é feita pela função `auth.purge_audit_events(before)`, `SECURITY DEFINER` do `auth_migrator`, chamada pelo job da 2026092515.
7. **LGPD.** IP e user agent são dados pessoais. A base legal é segurança e prevenção a fraude, e a retenção de 1 ano fica documentada.
8. **Sem endpoint de consulta por enquanto.** A consulta é por SQL, e um relatório fica para spec futura. Índices em `(user_external_id, occurred_at)` e em `occurred_at`.

## Tarefas

- [ ] **Dev** — Criar `IAuditLog` e o contexto de requisição (IP, user agent) na Application, montado pela Api; registrar cada evento da decisão 2 nos interactors, com sucesso dentro da transação e falha fora dela
- [ ] **DBA** — Migration de `auth.audit_events` com `CHECK` de `event_type` e `outcome`, índices, grants só de `INSERT`/`SELECT` para `auth_service` e a função `auth.purge_audit_events(before)` `SECURITY DEFINER`; criar o `DapperAuditLog`
- [ ] **Dev** — Chamar a purga (retenção de 1 ano, configurável) no job da 2026092515
- [ ] **Tester** — Unitários: cada fluxo registra o evento certo; nenhum evento carrega senha, token ou login digitado de conta inexistente. Integração: um rollback remove o evento de sucesso e mantém o de falha; `auth_service` não consegue `UPDATE` nem `DELETE` na tabela; a purga respeita a retenção
- [ ] **Tech Writer** — Criar doc em `docs/auth/` com o catálogo de eventos, o que nunca é registrado, a retenção, a base LGPD e consultas SQL de exemplo
