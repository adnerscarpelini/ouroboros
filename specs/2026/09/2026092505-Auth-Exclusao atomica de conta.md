# 2026092505 - Exclusao atomica de conta

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, a exclusão de conta apareceu com escritas separadas: a conta pode ser marcada como excluída e a revogação dos tokens falhar. O usuário aprovou tornar a exclusão consistente, inclusive na regra do último Admin.

## Análise

Depende de 2026092516 (unidade de trabalho). As regras da spec 2026092306 continuam: exclusão da própria conta ou por Admin, senha de quem exclui obrigatória e último Admin ativo preservado.

Decisões:
1. **Uma transação** para a exclusão lógica, a revogação dos refresh tokens e a invalidação dos tokens pendentes de confirmação e de reset.
2. **Último Admin sob concorrência.** Quando o alvo é Admin ativo, a contagem de Admins ativos roda com `SELECT ... FOR UPDATE` sobre essas linhas, dentro da transação. Duas exclusões simultâneas de Admins ficam em fila, e a segunda já enxerga a contagem reduzida. A troca de perfil (2026092520) reaproveita o mesmo método para o rebaixamento.
3. **Falha de senha conta para o bloqueio** (2026092501). Ela é gravada fora da transação da exclusão, para não ser desfeita pelo rollback.
4. A autorização pelo perfil gravado no banco, em vez do claim, está na spec 2026092510.

## Tarefas

- [ ] **Dev** — Executar `Delete()`, a atualização do usuário, `RevokeAllActiveByUserAsync` e as duas `InvalidatePendingByUserAsync` dentro do `IUnitOfWork`, mantendo reautenticação e autorização antes da transação
- [ ] **DBA** — Trocar `CountActiveAdminsAsync` por uma contagem com bloqueio (`FOR UPDATE`) para uso dentro da transação
- [ ] **Tester** — Integração: falha forçada na revogação → conta continua ativa e tokens intactos; dois Admins, cada um excluindo o outro ao mesmo tempo → exatamente um sucesso e um `400`
- [ ] **Tech Writer** — Atualizar `docs/auth/0007 - Exclusao de Conta.md` com a garantia de consistência e a regra concorrente do último Admin
