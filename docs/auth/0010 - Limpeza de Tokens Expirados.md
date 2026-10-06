# Limpeza de Tokens Expirados

Sem limpeza, `auth.tokens` e `auth.refresh_tokens` só crescem. O `ExpiredTokenCleanupService`, um `BackgroundService` do próprio auth-service, apaga periodicamente as linhas expiradas.

## O que é apagado

Linhas das duas tabelas com `expires_at < agora - Retention` (padrão: 30 dias depois de expirar).

| Linha | Destino |
|---|---|
| Expirada há mais que a retenção (usada, revogada ou não) | Apagada |
| Expirada há menos que a retenção | Fica |
| Ativa (não expirada) | Fica, sempre |
| Revogada ou usada, mas ainda não expirada | Fica. A detecção de reuso (ver `docs/auth/0003 - Login e Tokens.md`) precisa dos refresh tokens revogados até eles expirarem |

- O histórico de longo prazo de segurança fica na auditoria, não nestas tabelas.
- A regra de cadastro abandonado (ver `docs/auth/0001 - Confirmacao de Cadastro.md`) só olha tokens pendentes, então apagar os expirados não muda nada nela.
- A limpeza não mexe em `auth.users`.

## Como roda

- A cada `TokenCleanup:Interval` (padrão 1 h), com um primeiro ciclo ao subir. Usa `PeriodicTimer`.
- Cada ciclo apaga em **lotes de 1.000** (`DELETE TOP (1000) ... WHERE expires_at < @corte`). Cada lote é uma transação curta, e o ciclo segue enquanto um lote vier cheio, primeiro numa tabela e depois na outra. Isso evita locks longos.
- O tamanho do lote é uma constante (`CleanupExpiredTokensInteractor.BatchSize`), não configuração.
- Os dois `DELETE` usam o índice em `expires_at` (`tokens_expires_at_idx` e `refresh_tokens_expires_at_idx`, migration `V20260930100000__AddExpiresAtIndexes.sql`). Como toda escrita no projeto, sem hint de leitura.
- **Uma instância por vez não é garantida.** Apagar expirados é idempotente: duas instâncias (ou dois ciclos) rodando juntas não corrompem nada, só repetem trabalho. O serviço roda numa instância só hoje, e um lock entre réplicas (`sp_getapplock`) fica para quando houver réplicas e a disputa incomodar.
- A purga da auditoria não roda neste job. A retenção dela é tratada na própria spec da auditoria.

## Configuração

Seção `TokenCleanup` do `appsettings.json` (variáveis de ambiente: `TokenCleanup__Enabled` etc.):

| Chave | Padrão | Significado |
|---|---|---|
| `TokenCleanup:Enabled` | `true` | Liga ou desliga o serviço. Desligado, ele só registra `Information` e termina |
| `TokenCleanup:Interval` | `01:00:00` | Intervalo entre ciclos |
| `TokenCleanup:Retention` | `30.00:00:00` | Quanto tempo a linha fica depois de expirar |

`Interval` e `Retention` precisam ser maiores que zero. A configuração é validada no startup (`ValidateOnStart`): um valor inválido derruba a API com `OptionsValidationException` e a mensagem da chave.

Os testes de integração mantêm a limpeza desligada, exceto nos testes dela.

## Operação

- **Log de cada ciclo:** `Information`, `Expired token cleanup removed {Tokens} tokens and {RefreshTokens} refresh tokens`. Só contagens, nunca valores de token.
- **Falha:** o ciclo registra `Error` (`Expired token cleanup failed; it will try again in the next cycle`) e o serviço continua. O ciclo seguinte tenta de novo. A API não cai.
- **Encerramento:** o serviço para entre dois lotes quando a API é desligada.
- **Desligar sem redeploy:** `TokenCleanup__Enabled=false`.
- **Conferir o que sobrou:**
  ```sql
  SELECT COUNT(*) FROM auth.tokens WHERE expires_at < DATEADD(DAY, -30, SYSDATETIMEOFFSET());
  SELECT COUNT(*) FROM auth.refresh_tokens WHERE expires_at < DATEADD(DAY, -30, SYSDATETIMEOFFSET());
  ```
  Os dois devem ser zero logo depois de um ciclo.

## Onde está no código

- Serviço: `Auth.Api/Services/ExpiredTokenCleanupService.cs`.
- Configuração e validação: `Auth.Api/Configuration/TokenCleanupSettings.cs`.
- Caso de uso (laço de lotes): `Auth.Application/UseCases/CleanupExpiredTokens/`.
- Exclusão em lote: `DapperTokenRepository.DeleteExpiredBatchAsync` e `DapperRefreshTokenRepository.DeleteExpiredBatchAsync`.
