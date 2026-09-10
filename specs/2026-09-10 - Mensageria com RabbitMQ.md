---
title: Mensageria com RabbitMQ
state: new
---

# Mensageria com RabbitMQ

## Contexto

O serviço de Notificações já existe e funciona de ponta a ponta a partir do próprio banco: valida a
solicitação, deduplica, renderiza, entrega por SMTP, registra tentativa e expira o que venceu. O Auth
já grava a solicitação em `common.outbox_messages`, dentro da transação de negócio.

Falta o pedaço entre os dois: **nada leva a mensagem da outbox do produtor até o consumidor.** Esta
spec cobre exatamente esse pedaço, separado da
[spec do serviço de Notificações](archived/2026-09-09%20-%20Servico%20de%20Notificacoes.md) a pedido do
usuário.

Consequência de hoje, e que esta entrega encerra: **e-mail não é entregue em desenvolvimento.** As
solicitações ficam em `common.outbox_messages` com `status = 0` (`Pending`), e o
`OutboxPublisherProcessor` avisa uma vez na subida que não há transporte registrado e não roda.

## O que já está pronto

O ponto de encaixe é uma interface de cada lado, e as duas já existem:

| Encaixe | Onde | O que falta |
|---|---|---|
| `IMessagePublisher` | `BuildingBlocks.Application` | A implementação RabbitMQ, e registrá-la no `Program.cs` do produtor. |
| `IEmailDeliveryIntakeService` | `Notifications.Application` | O consumidor que lê a fila e chama isto, confirmando o consumo só depois do commit local. |

Nada além disso deve precisar mudar em `BuildingBlocks`, no Auth ou nas camadas de negócio de
Notificações. Se a implementação exigir mudar caso de uso ou entidade, é sinal de que o desenho
precisa de revisão — registrar aqui antes de seguir.

`OutboxPublisher<TDbContext>` já é registrado por fábrica justamente para que a ausência de
`IMessagePublisher` não derrube a Api na validação de serviços. Ao registrar o transporte, conferir
que o processador passa a rodar de fato (o aviso de "nenhum transporte registrado" deve sumir do log).

## Escopo

1. **Broker no Compose.** Serviço RabbitMQ fora do profile `apps` (é infraestrutura, como o Postgres e
   o Mailpit), com volume, health check e `restart: unless-stopped`. Portas de desenvolvimento
   vinculadas a localhost; interface de administração nunca exposta publicamente. Imagem e versão
   fixadas e validadas na implementação.
2. **Topologia provisionada antes dos produtores.** Exchange durável `notifications`, routing key
   `email.requested.v1`, fila `notifications.email.requests.v1`, mensagens persistentes e fila durável
   de quarentena. Provisionar por identidade administrativa separada, não pela credencial da aplicação.
3. **Publisher.** Implementação de `IMessagePublisher` com *publisher confirms*, `mandatory` (mensagem
   sem rota é falha, não sucesso silencioso) e reconexão com espera. Confirmação do broker não
   significa consumo.
4. **Consumer.** `BackgroundService` em Notificações que lê a fila, valida `MessageType`/`SchemaVersion`
   **antes** de desserializar, chama `IEmailDeliveryIntakeService` e só então dá ACK manual. Resultado
   `Conflict` ou `Rejected` vai para quarentena, com confirmação da quarentena antes do ACK da original.
5. **Credenciais separadas por papel.** O Auth publica somente na rota permitida; Notificações consome
   a própria fila e publica quarentena. Validar a origem pela rota/credencial, não apenas pelo campo
   `Producer` do corpo. Vhost por ambiente, TLS, credenciais fora de arquivo versionado.
6. **Correlação.** O `CorrelationId` já é persistido na outbox e viaja no `MessageEnvelope`: relê-lo no
   consumidor e colocá-lo no log estruturado de publicação, ingestão e tentativa. Nunca logar token
   nem payload.
7. **Testes.** Cobertura de publicação (confirms, falha, reconexão) e de ingestão pelo consumidor
   (reentrega, versão desconhecida, quarentena), seguindo a [ags-qa](../.claude/skills/ags-qa/SKILL.md).
   Testar com **duas instâncias** do consumidor desde o início, para não descobrir duplicação trivial
   só em reinício sobreposto.
8. **Documentação.** Atualizar a seção "Estado atual: sem transporte" de
   [docs/0007](../docs/0007%20-%20Fila%20de%20E-mails%20%28Outbox%29.md), mais
   [docs/0006](../docs/0006%20-%20Rodando%20a%20Stack%20em%20Containers.md), o `.env.example` e a skill
   [ags-devops](../.claude/skills/ags-devops/SKILL.md).

## Fora de escopo

- Alta disponibilidade do broker: um nó em desenvolvimento não a oferece, e topologia replicada é outra
  entrega.
- Segundo produtor. Vendas exercitaria o desenho de verdade, mas o serviço ainda não existe.
- API pública de reenvio ou consulta de entregas. Continua sendo ferramenta administrativa, e exige
  definir autorização antes — o host de Notificações hoje não registra autenticação.
- Backend de métricas. Os indicadores de operação (pendentes, idade da mais antiga, taxa de falha,
  quarentena) começam como consulta e log documentados.

## Decisões pendentes

| Decisão | Proposta | O que depende dela |
|---|---|---|
| Cliente .NET | Cliente oficial RabbitMQ, direto, sem framework de barramento | Pacotes, código de plumbing, testes de transporte. Um framework com outbox própria não pode conviver com a que já existe sem desenho explícito. |
| Tipo de fila | Quorum durável com volume persistente | Topologia e o que se promete sobre perda de mensagem. |
| Política de retentativa da publicação | Espera exponencial com jitter de 1s a 5min, sem descarte — já implementada em `OutboxOptions` | Confirmar os valores atuais ou ajustá-los. |
| Limpeza da outbox | Apagar payload de mensagens publicadas depois de janela definida | Retenção, replay e a política de dados sensíveis (URLs com token atravessam a outbox). |
| Quarentena | Fila durável, preservando id e impressão digital | Procedimento de operação e alerta. |

## Critérios de aceite

- `docker compose up -d` sobe o broker junto de Postgres e Mailpit, com health check verde.
- Cadastro pelo gateway resulta em e-mail visível no Mailpit em até uma rodada de polling, com a
  linha da outbox em `Published` e a entrega em `AcceptedByProvider`.
- Broker derrubado: o cadastro continua sendo aceito, a mensagem fica pendente e é publicada sozinha
  quando o broker volta — sem intervenção e sem perda.
- Notificações derrubado no meio do consumo: ao voltar, a reentrega não gera um segundo e-mail.
- Mensagem com versão desconhecida vai para quarentena e não é descartada em silêncio.
- `auth_service` continua sem conseguir conectar ao banco de Notificações, e vice-versa.
