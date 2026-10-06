# 2026092515 - Limpeza de tokens expirados

**Data:** 25/09/2026
**Servico(s):** auth-service

## Solicitação

Na validação de maturidade do auth-service, `auth.tokens` e `auth.refresh_tokens` apareceram crescendo sem limite, porque nada remove as linhas expiradas. O usuário aprovou uma limpeza periódica, com retenção e agendamento definidos.

## Análise

Extensão do `auth-service`. A prioridade é baixa, porque o volume atual é pequeno. Referências: `BackgroundService` do .NET e application locks do SQL Server (`sp_getapplock`).

Decisões:
1. **`ExpiredTokenCleanupService`**, um `BackgroundService` no próprio auth-service, roda a cada 1 h.
2. **Descartado: `sp_getapplock` para uma execução por vez entre réplicas.** Hoje existe uma única instância, e apagar linhas expiradas é idempotente: duas réplicas rodando juntas não corrompem nada, só repetem trabalho. Se o serviço ganhar réplicas e a disputa incomodar, o lock entra em spec própria.
3. **Retenção de 30 dias depois de expirar.** São apagadas as linhas das duas tabelas com `expires_at < now() - 30 dias`.
   - Tokens ativos nunca são tocados.
   - Tokens revogados que ainda não expiraram também ficam, porque a detecção de reuso (2026092507) precisa deles até expirarem.
   - O histórico de longo prazo fica na auditoria (2026092519).
4. **Em lotes de 1.000** (`DELETE TOP (1000) ... WHERE ...`). Cada lote é uma transação curta, e o ciclo segue até não sobrar nada. Isso evita locks longos.
5. **Índice em `expires_at`** nas duas tabelas.
6. **Configuração `TokenCleanup:Enabled`, `Interval` e `Retention`**, validada no startup. O tamanho do lote (1.000) é constante. Fica desligada nos testes de integração, exceto nos da própria limpeza.
7. **Observabilidade.** Cada ciclo loga em `Information` a contagem por tabela, nunca valores de token. Uma falha gera `Error`, e o serviço tenta de novo no ciclo seguinte.
8. **Cadastro abandonado não é afetado.** A regra da spec 2026092305 só olha tokens pendentes, então apagar os expirados não muda nada nela.
9. **A purga da auditoria (2026092519) não roda neste job.** A retenção da auditoria é tratada na própria spec dela.
