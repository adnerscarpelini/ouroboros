# 2026092515 - Limpeza de tokens expirados

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, `auth.tokens` e `auth.refresh_tokens` apareceram crescendo sem limite, porque nada remove as linhas expiradas. O usuário aprovou uma limpeza periódica, com retenção e agendamento definidos.

## Análise

Extensão do `auth-service`. A prioridade é baixa, porque o volume atual é pequeno. Referências: `BackgroundService` do .NET e application locks do SQL Server (`sp_getapplock`).

Decisões:
1. **`ExpiredTokenCleanupService`**, um `BackgroundService` no próprio auth-service, roda a cada 1 h.
2. **Uma execução por vez entre réplicas.** O serviço usa `sp_getapplock` (modo `Exclusive`, `@LockTimeout = 0`) com um recurso fixo. A réplica que não consegue o lock pula o ciclo.
3. **Retenção de 30 dias depois de expirar.** São apagadas as linhas das duas tabelas com `expires_at < now() - 30 dias`.
   - Tokens ativos nunca são tocados.
   - Tokens revogados que ainda não expiraram também ficam, porque a detecção de reuso (2026092507) precisa deles até expirarem.
   - O histórico de longo prazo fica na auditoria (2026092519).
4. **Em lotes de 1.000** (`DELETE TOP (1000) ... WHERE ...`). Cada lote é uma transação curta, e o ciclo segue até não sobrar nada. Isso evita locks longos.
5. **Índice em `expires_at`** nas duas tabelas.
6. **Configuração `TokenCleanup:Enabled`, `Interval`, `Retention` e `BatchSize`**, validada no startup. Fica desligada nos testes de integração, exceto nos da própria limpeza.
7. **Observabilidade.** Cada ciclo loga em `Information` a contagem por tabela, nunca valores de token. Uma falha gera `Error`, e o serviço tenta de novo no ciclo seguinte.
8. **Cadastro abandonado não é afetado.** A regra da spec 2026092305 só olha tokens pendentes, então apagar os expirados não muda nada nela.
9. **A purga da auditoria (2026092519) roda neste mesmo job**, com retenção própria.

## Tarefas

- [ ] **Dev** — Criar o `ExpiredTokenCleanupService` com advisory lock, laço de lotes, opções `TokenCleanup:*` validadas e logs de contagem
- [ ] **DBA** — Migration com índices em `expires_at` em `auth.tokens` e `auth.refresh_tokens`; métodos de exclusão em lote nos dois repositórios
- [ ] **Tester** — Integração: expirados há mais de 30 dias são apagados; ativos, revogados não expirados e expirados recentes ficam; duas execuções simultâneas → só uma trabalha; volume maior que um lote é processado por inteiro
- [ ] **Tech Writer** — Criar doc em `docs/auth/` com a retenção, o agendamento, a configuração e a operação da limpeza
