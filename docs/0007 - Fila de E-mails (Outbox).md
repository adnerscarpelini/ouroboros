# 0007 - Fila de E-mails (Outbox)

## Contexto

Enviar e-mail é uma chamada de rede a um sistema de terceiros, e ela acontece no meio de um caso de uso que também grava no banco. Fazer as duas coisas juntas dá errado dos dois lados:

- Se o e-mail for enviado antes do commit e a transação falhar, o usuário recebe "confirme seu cadastro" de um cadastro que não existe.
- Se for enviado depois do commit e o servidor SMTP estiver fora do ar, o usuário é criado sem nenhum caminho para se ativar.

Nenhum dos dois é resolvível com `try/catch`: são dois sistemas diferentes, sem transação em comum.

## Decisão

Usar o padrão **Outbox**, dividido em duas metades que nunca se falam diretamente:

1. **Enfileirar** (`IEmailQueueService`): o caso de uso grava a mensagem na tabela `common.email_messages` **dentro da mesma transação** do dado de negócio. Ou o usuário e o e-mail a enviar são gravados juntos, ou nada é.
2. **Entregar** (`EmailOutboxProcessor`): um `BackgroundService` varre a fila periodicamente, fora daquela transação, e entrega o que estiver pendente.

O caso de uso nunca espera pelo servidor SMTP. Um SMTP fora do ar atrasa o e-mail; não derruba o cadastro.

## As peças

| Peça | Onde | Papel |
|---|---|---|
| `EmailMessage` | `BuildingBlocks.Domain` | A linha da fila: destinatário, assunto, corpo, se já foi enviada e o histórico de tentativas. |
| `IEmailQueueService` | `BuildingBlocks.Application` | Enfileira. Chamado pelo caso de uso, dentro da transação dele. |
| `IEmailSender` | `BuildingBlocks.Application` | Entrega de fato. Só o processador chama. |
| `EmailOutboxDispatcher<TDbContext>` | `BuildingBlocks.Infrastructure` | Uma passada pela fila: pega um lote, tenta entregar, grava o resultado. |
| `EmailOutboxProcessor<TDbContext>` | `BuildingBlocks.Infrastructure` | `BackgroundService` que chama o dispatcher de tempos em tempos. |
| `SmtpEmailSender` | `BuildingBlocks.Infrastructure` | Implementação SMTP (MailKit). |

`Dispatcher` e `Processor` são separados de propósito: um sabe **o que fazer**, o outro **de quanto em quanto tempo**. É o que permite testar o despacho sem depender de temporizador.

O parâmetro de tipo `TDbContext` mantém a regra de isolamento: cada serviço processa a própria fila, na própria base. `BuildingBlocks` continua sendo só código.

## Tentativas e mensagens problemáticas

Cada linha guarda `attempt_count`, `last_attempt_at` e `last_error`:

- Falhou? A tentativa é registrada na própria linha e a mensagem volta na rodada seguinte.
- Uma mensagem que falha não derruba o lote — as outras do mesmo lote continuam sendo entregues.
- Ao atingir `MaxAttempts`, a mensagem **sai da fila** em vez de bater no servidor SMTP para sempre. Ela permanece na tabela, com o último erro, para inspeção.

## Configuração

Seção `EmailOutbox` do `appsettings.json` do serviço:

```json
"EmailOutbox": {
  "SmtpHost": "localhost",
  "SmtpPort": 1025,
  "FromAddress": "nao-responda@ouroboros.local",
  "FromName": "Ouroboros",
  "PollingInterval": "00:00:15",
  "BatchSize": 20,
  "MaxAttempts": 5
}
```

Ligado no `Program.cs` do serviço, ao lado do `AddCommon`:

```csharp
builder.Services.AddCommon<AuthDbContext>();
builder.Services.AddEmailOutbox<AuthDbContext>(emailOutboxOptions);
```

`AddEmailOutbox` é separado do `AddCommon` porque um serviço pode enfileirar e-mail sem ser ele quem entrega — ou não usar e-mail nenhum.

## Servidor SMTP de desenvolvimento

O `docker-compose.yml` sobe o **Mailpit**, que aceita qualquer e-mail e mostra numa interface web sem entregar nada de verdade:

- SMTP: `localhost:1025`
- Interface web: **http://localhost:8025**

Ele fica fora do profile `apps` porque o fluxo pela IDE também precisa de um destino SMTP local. Dentro da rede do Compose, o host é `mailpit`; fora dela, `localhost`.

É por ali que se pega o link de confirmação de cadastro ou de redefinição de senha durante o desenvolvimento.

## Consequências

- A entrega é assíncrona: existe uma janela entre o cadastro e o e-mail chegar, limitada por `PollingInterval`.
- Uma mensagem pode ser entregue mais de uma vez se o processo cair entre o envio e o `SaveChanges` que a marca como enviada. É o comportamento *at-least-once*, normal em Outbox — e aceitável para e-mail, onde receber duas vezes é bem menos grave que não receber.
- A varredura é por *polling*, não por evento. Com uma instância por serviço isso basta; com várias instâncias do mesmo serviço, duas poderiam pegar o mesmo lote — resolver isso pede trava no banco (`FOR UPDATE SKIP LOCKED`), o que só vale a pena quando houver mais de uma instância.
