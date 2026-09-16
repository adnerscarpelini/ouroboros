# 0007 - Fila de E-mails (Outbox)

## Contexto

Enviar e-mail é uma chamada de rede a um sistema de terceiros, e ela acontece no meio de um caso de uso que também grava no banco. Fazer as duas coisas juntas dá errado dos dois lados:

- Se o e-mail for enviado antes do commit e a transação falhar, o usuário recebe "confirme seu cadastro" de um cadastro que não existe.
- Se for enviado depois do commit e o servidor SMTP estiver fora do ar, o usuário é criado sem nenhum caminho para se ativar.

Nenhum dos dois é resolvível com `try/catch`: são dois sistemas diferentes, sem transação em comum.

Havia um segundo problema, que só apareceria no segundo serviço: template, retentativa, credencial de SMTP e histórico de envio viviam dentro do Auth. Um serviço de Vendas precisando mandar e-mail ganharia uma cópia física de tudo isso, e continuaria não existindo lugar nenhum onde alguém visse o que foi enviado a quem.

## Decisão

Duas metades que nunca se falam diretamente, agora em **dois serviços**:

1. **Solicitar** (`IOutboxMessageQueue`, em `BuildingBlocks`): o caso de uso grava a solicitação em `common.outbox_messages` **dentro da mesma transação** do dado de negócio. Ou o usuário e a solicitação de e-mail são gravados juntos, ou nada é.
2. **Entregar** (serviço `Notifications`): processo e banco próprios, responsáveis por template, provedor, retentativa e histórico.

Entre os dois, mensageria. O produtor publica depois do commit; nunca escreve no banco do outro serviço.

**A regra que sustenta o desenho: a linha da outbox nunca sai do banco do produtor.** O que atravessa a fronteira é a mensagem publicada depois do commit. É isso que permite centralizar a entrega sem compartilhar banco — que seria o anti-padrão *shared database*, e traria de volta o problema do contexto acima na forma de duas transações sem atomicidade entre si.

## O fluxo

O mesmo fluxo desenhado está em [docs/excalidraw/0007 - Fila de E-mails (Outbox).excalidraw](excalidraw/0007%20-%20Fila%20de%20E-mails%20%28Outbox%29.excalidraw).

```
Auth                                    Notifications
────                                    ─────────────
caso de uso
  ├─ grava User            ┐
  └─ grava outbox_messages ┘ mesma transação

publisher (background)
  └─ publica ─────────────► [transporte] ─────► consumer
                                                  └─ grava email_deliveries  (commit local)
                                                  └─ confirma o consumo      (só depois)

                                                dispatcher (background)
                                                  └─ renderiza + envia ──► SMTP
```

## As peças

| Peça | Onde | Papel |
|---|---|---|
| `OutboxMessage` | `BuildingBlocks.Domain` | A linha da outbox: tipo, versão, payload, correlação e estado da publicação. |
| `IOutboxMessageQueue` | `BuildingBlocks.Application` | Enfileira. Chamado pelo caso de uso, dentro da transação dele. |
| `IMessagePublisher` | `BuildingBlocks.Application` | O transporte. Sem implementação registrada, nada é publicado. |
| `OutboxPublisherService` | `BuildingBlocks.Infrastructure` | Uma passada SQL pela outbox: pega um lote, tenta publicar, grava o resultado. |
| `OutboxPublisherProcessorService` | `BuildingBlocks.Infrastructure` | `BackgroundService` que chama o publisher de tempos em tempos. |
| `EmailNotificationRequestedV1` | `Contracts.Notifications` | O contrato entre produtor e Notificações. Só DTO, sem dependência de projeto. |
| `IAcceptEmailDeliveryUseCase` | `NotificationsService.Application` | A porta de entrada do consumidor: valida, deduplica e persiste. |
| `EmailDelivery` | `NotificationsService.Domain` | A entrega em si, com estado, tentativas e conteúdo já renderizado. |
| `EmailDeliveryDispatcherService` | `NotificationsService.Infrastructure` | Uma passada pela fila de entregas: pega um lote, entrega, grava o resultado. |
| `SmtpEmailSenderService` | `NotificationsService.Infrastructure` | Implementação SMTP (MailKit). Só Notificações conhece servidor de e-mail. |

`Dispatcher`/`Publisher` e `Processor` são separados de propósito: um sabe **o que fazer**, o outro **de quanto em quanto tempo**. É o que permite testar o despacho sem depender de temporizador.

Cada produtor publica a própria outbox, na própria base, usando sua própria conexão SQL. `BuildingBlocks` continua sendo só código.

## Quem decide o quê

O Auth decide **por que**, **para quem** e **com quais dados** o e-mail deve ser enviado. Ele não monta HTML, não conhece SMTP e não espera pela entrega. Notificações decide **como** o e-mail fica e **quando** sai, e nunca consulta o produtor para completar um dado que faltou.

Uma consequência prática: o instante de expiração do link é calculado **uma única vez** no produtor, e vale para o token e para a solicitação. Calculado em dois lugares, a notificação poderia continuar sendo tentada depois de o link já ter morrido.

## Idempotência

O transporte é *at-least-once*: a mesma mensagem chegando duas vezes é comportamento normal, não erro. Quem absorve isso é a restrição única `(producer, request_id)` em `notifications.email_deliveries`:

- Mesma chave, mesmo conteúdo → reentrega. O consumo é confirmado sem criar um segundo envio.
- Mesma chave, conteúdo diferente → conflito. Nunca sobrescrever uma entrega existente.
- Chaves diferentes → dois e-mails, e está certo: dois pedidos de recuperação de senha são dois e-mails.

`exactly-once` não existe neste caminho. Um timeout depois de o SMTP ter aceitado a mensagem deixa o resultado genuinamente desconhecido, e repetir pode duplicar o e-mail — ver [RFC 5321](https://www.rfc-editor.org/rfc/rfc5321), seções 4.5.3.2 e 6.1.

## Tentativas e falhas

São três políticas separadas, e uma não consome o orçamento da outra:

| Etapa | Comportamento |
|---|---|
| Publicação da outbox | Espera exponencial com jitter, sem descarte. Transporte fora do ar atrasa a notificação; não a perde. |
| Ingestão pelo consumidor | Confirma o consumo só depois do commit local. Perdeu a confirmação? A reentrega esbarra na chave única. |
| Entrega SMTP | Até `MaxAttempts`, com espera crescente. Depois disso a entrega fica `Failed`, com o último erro, para inspeção. |

Depois que a solicitação foi aceita, falha de SMTP **nunca** devolve a mensagem para a fila do transporte: quem retoma é o dispatcher, a partir do banco de Notificações.

A validade é reconferida antes de cada envio, inclusive depois de um restart. Vencida, a entrega vira `Expired` em vez de virar um link morto na caixa de entrada.

## Diagnóstico

O `X-Correlation-Id` posto pelo Api Gateway é persistido na outbox, viaja no envelope e é relido pelo consumidor. É ele que liga, no log, a requisição de cadastro à tentativa de entrega que aconteceu minutos depois, em outro processo. Ele não substitui o `MessageId` como chave de idempotência — são coisas diferentes.

`common.error_logs` continua local em cada banco, inclusive no de Notificações: é erro técnico do próprio serviço, não histórico de tentativa de negócio. Esse histórico mora em `notifications.email_delivery_attempts`, que é durável — log de processo é volátil e não serve para responder "por que este e-mail não chegou".

## Estado atual: sem transporte

**O transporte ainda não existe.** A escolha e a implementação de um broker (RabbitMQ ou outro) são uma entrega própria, ainda não iniciada.

Enquanto isso:

- O Auth grava normalmente em `common.outbox_messages`, dentro da transação. Nada se perde.
- Nenhum `IMessagePublisher` está registrado, então o `OutboxPublisherProcessorService` avisa uma vez na subida e não roda. É melhor do que falhar de 15 em 15 segundos contra um transporte que ninguém registrou.
- Notificações está de pé e entrega tudo que estiver persistido no banco dele — só não chega nada novo.

Ou seja: **e-mail não é entregue em desenvolvimento até a mensageria existir**. As solicitações ficam visíveis em `common.outbox_messages`, com `status = 0` (`Pending`).

## Servidor SMTP de desenvolvimento

O `docker-compose.yml` sobe o **Mailpit**, que aceita qualquer e-mail e mostra numa interface web sem entregar nada de verdade:

- SMTP: `localhost:1025`
- Interface web: **http://localhost:8025**

Ele fica fora do profile `apps` porque o fluxo pela IDE também precisa de um destino SMTP local. Dentro da rede do Compose, o host é `mailpit`; fora dela, `localhost`. Quem fala com ele agora é o serviço de Notificações — o Auth não tem mais nem a configuração.

## Configuração

Produtor (`appsettings.json` do serviço), seção `Outbox`:

```json
"Outbox": {
  "PollingInterval": "00:00:15",
  "BatchSize": 20,
  "InitialRetryDelay": "00:00:01",
  "MaxRetryDelay": "00:05:00"
}
```

Ligada no `Program.cs`, ao lado do `AddCommon`:

```csharp
builder.Services.AddCommon();
builder.Services.AddTransactionalOutbox(
	producer: new OutboxProducer(NotificationProducers.Auth),
	options: outboxOptions
);
```

O nome do produtor vem de código, não do `appsettings`: é identidade validada do outro lado contra um catálogo fechado, e um erro de digitação em arquivo de configuração viraria mensagem rejeitada.

Notificações tem as seções `EmailDelivery` (lote, tentativas, esperas) e `Smtp` (host, porta, remetente, política de TLS, credencial opcional).

## Consequências

- E-mail deixa de ser "enviado logo após o commit" e passa a ser "aceito para envio". O link ainda pode expirar antes de chegar.
- Duplicação é possível: sem a chave de idempotência, o usuário receberia o mesmo e-mail duas vezes.
- Mais um processo, mais um banco e mais um deploy para operar.
- Em troca, um segundo produtor ganha e-mail escrevendo numa outbox e publicando — sem template, sem SMTP, sem credencial de provedor —, e passa a existir um lugar único onde se vê o que foi enviado a quem.
