# Auditoria de Eventos de Segurança

Os eventos de autenticação e de conta ficam numa trilha própria, a tabela `auth.audit_events`, **só de inserção**. O Serilog continua registrando o dia a dia (e vai para o Seq), mas a auditoria é a trilha que prova o que aconteceu.

## Tabela `auth.audit_events`

| Coluna | Conteúdo |
|---|---|
| `id`, `external_id` | Identificação do evento |
| `occurred_at` | Quando aconteceu (UTC) |
| `event_type` | Tipo do evento (catálogo abaixo). `CHECK` com os valores válidos |
| `outcome` | `Success` ou `Failure`. `CHECK` |
| `user_external_id` | Conta afetada. **Sem FK**, para o evento sobreviver à remoção de um cadastro abandonado |
| `actor_external_id` | Quem agiu, quando não é o próprio usuário (um Admin) |
| `session_id` | Sessão, quando existir |
| `ip_address` | `nvarchar(45)`, texto de IPv4 ou IPv6, já resolvido pelos forwarded headers confiáveis (ver `docs/auth/0008 - Protecao contra Tentativas de Autenticacao.md`) |
| `user_agent` | Truncado em 256 caracteres |
| `reason` | Código fixo (`AuditReason`), nunca texto livre |

Índices: `(user_external_id, occurred_at)` e `(occurred_at)`.

## Catálogo de eventos

| Evento | Outcome | Quando | Campos extras |
|---|---|---|---|
| `UserRegistered` | `Success` | Cadastro criado. E-mail já ocupado não gera evento | `user_external_id` |
| `EmailConfirmed` | `Success` | Confirmação de cadastro concluída | `user_external_id` |
| `LoginSucceeded` | `Success` | Login concluído | `user_external_id`, `session_id` |
| `LoginFailed` | `Failure` | Login recusado | `reason`: `invalid_password` (conta existe, senha errada), `unknown_login` (login inexistente), `locked_out` (conta bloqueada), `inactive_account` (senha certa, e-mail não confirmado) |
| `AccountLockedOut` | `Failure` | A falha que atingiu o limite e bloqueou a conta (no login, na troca de senha ou na exclusão de conta) | `user_external_id`, `reason = too_many_failed_attempts` |
| `RefreshTokenReuseDetected` | `Failure` | Refresh token já revogado apresentado de novo (ver `docs/auth/0003 - Login e Tokens.md`) | `user_external_id`, `session_id`, `reason = reuse_detected` |
| `Logout` | `Success` | Logout que revogou um token ativo. Logout repetido (idempotente) não gera evento | `user_external_id`, `session_id` |
| `LogoutAll` | `Success` | Encerramento de todas as sessões | `user_external_id` |
| `PasswordResetRequested` | `Success` | Solicitação de recuperação **só quando a conta existe e o link foi gerado**. Isso nunca é exposto ao cliente: a resposta é a mesma nos dois casos | `user_external_id` |
| `PasswordResetCompleted` | `Success` | Redefinição concluída | `user_external_id` |
| `PasswordChanged` | `Success` | Troca de senha autenticada | `user_external_id`, `session_id` (a atual) |
| `UserDeleted` | `Success` | Exclusão lógica de conta | `user_external_id` (a excluída), `actor_external_id` (o Admin, quando não foi auto-exclusão) |

- O **refresh normal não é auditado**, pelo volume, e já está no log.
- O `RoleChanged` não existe: a troca de perfil por endpoint está adiada (spec 2026092520). Quando vier, entra com uma migration que acrescenta o valor ao `CHECK`.

## O que nunca é registrado

- Senha (atual, nova ou digitada), token (de acesso, de refresh, de confirmação ou de recuperação) e hash.
- O **login digitado numa tentativa contra conta inexistente**. Usuários digitam a senha no campo de login por engano. Nesse caso `user_external_id` fica nulo e o `reason` é `unknown_login`.
- Mensagem de exceção ou qualquer texto livre: o `reason` vem de uma lista fixa de códigos.

O `AuditEvent` (Application) nem tem campo para esses dados. IP e user agent vêm do contexto da requisição.

## Onde cada evento é gravado

- **Evento de sucesso: na mesma transação da operação** (`IUnitOfWork`, ver `docs/project/0001 - Arquitetura.md`). Se a operação for desfeita, não há evento. Se o evento não puder ser gravado, a operação é desfeita.
- **Evento de falha: numa transação própria (autocommit)**, para sobreviver ao rollback. Os eventos de falha são gravados fora de qualquer `IUnitOfWork`.
- Logout, logout-all e solicitação de recuperação, que antes eram escritas soltas, passaram a rodar numa `IUnitOfWork` junto com o evento.

## Só inserção, garantido pelo banco

- O papel `auth_service` (o da API, ver `docs/project/0002 - Docker.md`) tem `INSERT` e `SELECT` na tabela e **nunca** `UPDATE`, `DELETE` nem `TRUNCATE`. A migration aplica `DENY UPDATE, DELETE ON auth.audit_events` ao papel que recebeu o `GRANT` no schema `auth`; o `DENY` no objeto vence o `GRANT` no schema.
- Quem faz DDL e retenção é o `auth_migrator` (`db_owner`, não afetado por `DENY`).
- Uma função de purga automática ficou **descartada**: o volume é mínimo hoje, e a purga automática entra em spec própria se o volume pedir.

## LGPD e retenção

- IP e user agent são **dados pessoais**. A base legal é **segurança e prevenção a fraude**.
- **Retenção de 1 ano.** É uma **tarefa operacional**, não um job do serviço: roda com o `auth_migrator`, por exemplo uma vez por mês. Exemplo, em lotes para não travar a tabela:

  ```sql
  -- Rodar como auth_migrator (o auth_service nao consegue DELETE).
  DECLARE @corte datetimeoffset = DATEADD(YEAR, -1, SYSDATETIMEOFFSET());

  WHILE 1 = 1
  BEGIN
      DELETE TOP (5000) FROM auth.audit_events WHERE occurred_at < @corte;

      IF @@ROWCOUNT = 0 BREAK;
  END;
  ```
- Os eventos não têm FK para a conta: excluir ou anonimizar uma conta não apaga a trilha. Se um pedido de titular exigir remoção, ela é feita à mão, com o mesmo papel, por `user_external_id`.

## Consultas de exemplo

A consulta é por SQL: não existe endpoint de consulta por enquanto, e um relatório fica para uma spec futura.

```sql
-- Linha do tempo de uma conta
SELECT occurred_at, event_type, outcome, reason, ip_address, session_id
FROM auth.audit_events
WHERE user_external_id = '3b8f55c5-155c-41a7-9949-e5b7d0d83ffd'
ORDER BY occurred_at DESC;

-- Falhas de login por IP na ultima hora (tentativa de forca bruta ou de credential stuffing)
SELECT ip_address, COUNT(*) AS falhas, COUNT(DISTINCT user_external_id) AS contas
FROM auth.audit_events
WHERE event_type = N'LoginFailed' AND occurred_at > DATEADD(HOUR, -1, SYSDATETIMEOFFSET())
GROUP BY ip_address
ORDER BY falhas DESC;

-- Contas bloqueadas no ultimo dia
SELECT occurred_at, user_external_id, ip_address
FROM auth.audit_events
WHERE event_type = N'AccountLockedOut' AND occurred_at > DATEADD(DAY, -1, SYSDATETIMEOFFSET());

-- Reuso de refresh token (possivel roubo de sessao)
SELECT occurred_at, user_external_id, session_id, ip_address, user_agent
FROM auth.audit_events
WHERE event_type = N'RefreshTokenReuseDetected'
ORDER BY occurred_at DESC;

-- Exclusoes feitas por um Admin
SELECT occurred_at, actor_external_id, user_external_id
FROM auth.audit_events
WHERE event_type = N'UserDeleted' AND actor_external_id IS NOT NULL;
```

## Onde está no código

- Gateways: `IAuditLog`, `AuditEvent` e `IRequestContext` em `Auth.Application/Gateways/`.
- Tipos e códigos: `AuditEventType`, `AuditOutcome` e `AuditReason` em `Auth.Domain/Entities/AuditEventType.cs`.
- Gravação: `Auth.Infrastructure/Persistence/DapperAuditLog.cs` (trunca o user agent e o IP).
- Contexto da requisição: `Auth.Api/Configuration/HttpRequestContext.cs`.
- Tabela, índices e `DENY`: `V20261001100000__CreateAuditEventsTable.sql`.
- Os interactors registram os eventos (`LoginInteractor`, `RefreshAccessTokenInteractor`, `LogoutInteractor` etc.).
