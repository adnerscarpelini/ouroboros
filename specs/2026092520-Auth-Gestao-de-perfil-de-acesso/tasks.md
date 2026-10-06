# 2026092520 - Gestao de perfil de acesso — Tarefas

- [ ] **Dev** — Criar `User.ChangeRole(role)` no domínio e o caso de uso `ChangeUserRole` (autorização pelo banco, reautenticação com bloqueio, validações do alvo, regra do último Admin, transação)
- [ ] **Dev** — Criar o endpoint `PUT /api/users/{externalId}/role` (`[Authorize]`) e a política de rate limit `user-role-change`; atualizar a collection Postman
- [ ] **DBA** — Reaproveitar a contagem de Admins ativos com `UPDLOCK, HOLDLOCK` (2026092505) no rebaixamento; persistir `role` no update
- [ ] **Tester** — Unitários: `User` → `403` sem consultar o alvo; senha errada; alvo inexistente, excluído e não confirmado; valor fora da allowlist; valor igual (idempotente); rebaixar o último Admin ativo, inclusive a si mesmo
- [ ] **Tester** — Integração: promoção seguida de refresh → token com `role=Admin`; o Admin rebaixado perde o privilégio na hora, mesmo com token antigo (2026092510); dois rebaixamentos simultâneos dos dois últimos Admins → um falha
- [ ] **Tech Writer** — Atualizar `docs/auth/0005 - Perfis de Acesso.md`: novo endpoint, regras, e o SQL só como runbook de bootstrap do primeiro Admin
