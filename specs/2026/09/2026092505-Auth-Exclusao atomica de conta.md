# 2026092505 - Exclusao atomica de conta

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar exclusão de conta consistente mesmo quando a revogação de tokens ou outra gravação falhar.

## Análise

O `DeleteUserInteractor` marca a conta como excluída e depois revoga refresh tokens e invalida tokens pendentes em comandos separados. Manter a spec 2026092306: exclusão própria ou por Admin, senha do solicitante exigida e último Admin ativo preservado. A verificação do último Admin também precisa resistir a exclusões concorrentes. O endpoint continua autenticado; usuário comum só age sobre a própria conta e não recebe dados de terceiros.

## Tarefas

- [ ] **Dev** — Agrupar exclusão lógica e invalidação dos tokens numa operação indivisível, preservando reautenticação e autorização existentes.
- [ ] **DBA** — Implementar transação e proteção concorrente da regra do último Admin ativo no PostgreSQL.
- [ ] **Tester** — Cobrir rollback em falha de revogação, duas exclusões simultâneas de Admin e tentativas de excluir conta alheia com usuário comum.
- [ ] **Tech Writer** — Atualizar `docs/auth/0007 - Exclusao de Conta.md` com a garantia de consistência e a janela de validade do JWT definida na spec 2026092510.
