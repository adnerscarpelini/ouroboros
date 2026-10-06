# 2026092505 - Exclusao atomica de conta — Tarefas

- [ ] **Dev** — Executar `Delete()`, a atualização do usuário, `RevokeAllActiveByUserAsync` e as duas `InvalidatePendingByUserAsync` dentro do `IUnitOfWork`, mantendo reautenticação e autorização antes da transação
- [ ] **DBA** — Trocar `CountActiveAdminsAsync` por uma contagem com bloqueio (`WITH (UPDLOCK, HOLDLOCK)`) para uso dentro da transação
- [ ] **Tester** — Integração: falha forçada na revogação → conta continua ativa e tokens intactos; dois Admins, cada um excluindo o outro ao mesmo tempo → exatamente um sucesso e um `400`
- [ ] **Tech Writer** — Atualizar `docs/auth/0007 - Exclusao de Conta.md` com a garantia de consistência e a regra concorrente do último Admin
