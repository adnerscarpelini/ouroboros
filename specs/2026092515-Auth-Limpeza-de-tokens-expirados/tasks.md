# 2026092515 - Limpeza de tokens expirados — Tarefas

- [ ] **Dev** — Criar o `ExpiredTokenCleanupService` com advisory lock, laço de lotes, opções `TokenCleanup:*` validadas e logs de contagem
- [ ] **DBA** — Migration com índices em `expires_at` em `auth.tokens` e `auth.refresh_tokens`; métodos de exclusão em lote nos dois repositórios
- [ ] **Tester** — Integração: expirados há mais de 30 dias são apagados; ativos, revogados não expirados e expirados recentes ficam; duas execuções simultâneas → só uma trabalha; volume maior que um lote é processado por inteiro
- [ ] **Tech Writer** — Criar doc em `docs/auth/` com a retenção, o agendamento, a configuração e a operação da limpeza
