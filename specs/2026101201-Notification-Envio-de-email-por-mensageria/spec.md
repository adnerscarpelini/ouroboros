# 2026101201 - Envio de e-mail por mensageria

**Data:** 12/10/2026
**Servico(s):** novo serviço: notification-service; infraestrutura Docker

## Solicitação

Criar as specs iniciais de uma solução simples para enviar os e-mails do auth-service por mensageria, com um serviço separado responsável pelo envio.

## Análise

O auth-service já gera e valida tokens, mas não envia e-mail. O novo notification-service cuida apenas de consumir solicitações e enviá-las por SMTP; não conhece usuários, não valida tokens e não acessa o banco do auth. Referências: [confirmações do RabbitMQ](https://www.rabbitmq.com/docs/confirms), [controle de acesso do RabbitMQ](https://www.rabbitmq.com/docs/access-control) e [Mailpit](https://mailpit.axllent.org/docs/).

Decisões:
1. **RabbitMQ no Docker**, com volume persistente, fila **classic durável** `notification.email.v1` e mensagens persistentes. Usar o exchange padrão, com routing key igual ao nome da fila. Classic, e não quorum, porque o ambiente tem um nó só e a fila quorum do RabbitMQ 4 tem limite padrão de 20 entregas, que descartaria mensagens no meio do retry da decisão 7. Cliente oficial `RabbitMQ.Client`, sem MassTransit ou similar.
2. **Provisionamento do broker por um serviço de uso único (`rabbitmq-init`)**, no mesmo espírito do `sqlserver-init`: cria a vhost, um usuário por serviço, as permissões e declara a fila. Os serviços não declaram a fila e não têm permissão de configuração. Se a fila não existir, o publicador recebe o retorno do `mandatory` e tenta de novo depois (spec 2026101202); o consumidor tenta reconectar até ela existir. O usuário `guest` não é usado; o administrador do broker vem do `.env`.
3. **notification-service como worker .NET**, sem API HTTP e sem banco. Manter Domain, Application e Infrastructure; usar `Notification.Worker` como projeto de entrada no lugar de Api. Essa adaptação da convenção de quatro projetos é intencional: o serviço recebe mensagens, não requisições HTTP. Nenhum ProjectReference entre serviços, nem biblioteca compartilhada de contratos nesta entrega.
4. **Contrato JSON v1:** `messageId` (GUID), `template` (`EmailConfirmation` ou `PasswordReset`), `recipientEmail`, `recipientName`, `token` e `expiresAt` (UTC, ISO 8601). Uma solicitação envia um e-mail a um destinatário. O mesmo messageId é preservado nas republicações. Propriedades AMQP: `message-id` = messageId, `content-type` = `application/json`, `delivery-mode` persistente e `expiration` com o tempo restante até expiresAt. Os produtores e o consumidor mantêm seus próprios DTOs compatíveis com esse contrato.
5. **Dois templates fixos**, pertencentes ao notification-service: confirmação de cadastro e recuperação de senha. Assunto e texto simples, em inglês (mesmo idioma das mensagens da API), com nome, token e validade em UTC. O de recuperação avisa que uma nova solicitação invalida os códigos anteriores. Não receber HTML, assunto ou remetente arbitrários na mensagem. Remetente configurado no serviço. Como não existe frontend, esta primeira entrega usa o token recebido nas APIs existentes, via Postman; links e telas ficam para uma spec posterior.
6. **SMTP configurável e Mailpit local**, ambos no compose. Mailpit captura os e-mails para consulta no navegador, sem envio a destinatários reais. Host, porta, remetente, TLS e credenciais SMTP ficam na configuração; startup valida valores obrigatórios. O envio tem timeout de 30 segundos.
7. **Um consumidor, uma mensagem por vez**, com prefetch 1 e confirmação manual somente após o servidor SMTP aceitar o envio. Isso indica aceitação pelo SMTP, não entrega na caixa do destinatário.
8. **Falha no SMTP, separada por tipo:**
   - **Transitória** (conexão, timeout, resposta 4xx): aguardar 10 segundos e devolver a mensagem à fila. Repetir enquanto não expirar. Enquanto o SMTP estiver fora, a fila inteira espera; é aceito, porque a falha afeta todas as mensagens igual.
   - **Permanente** (resposta 5xx, como destinatário recusado): confirmar e descartar com log do motivo. Devolver à fila travaria todas as outras mensagens até a expiração (até 24h na confirmação de cadastro).
   - **Expirada** (expiresAt já passou): confirmar e descartar sem enviar.
   - **Inválida** (JSON, destinatário, template, token ou validade): rejeitar sem requeue e descartar com log do motivo.

   Não haverá fila de falhas, política sofisticada de retry ou painel de reprocessamento nesta entrega. Perda de conexão com o broker deve permitir reconexão e retomada do consumo.
9. **Duplicidade aceita nesta versão.** Se o SMTP aceitar e o worker cair antes do ack, ou se o produtor republicar, o mesmo e-mail pode chegar novamente. Não criar inbox, tabela de deduplicação ou promessa de envio exatamente uma vez. O token mantém seu uso único no auth.
10. **Proteção mínima (menor privilégio):** na vhost do Ouroboros, o usuário do notification-service só lê `notification.email.v1`; o do auth-service só escreve no exchange padrão (`amq.default`). Nenhum dos dois configura recursos. Nenhum endpoint público de envio. Portas do broker e da interface de administração publicadas só no compose de desenvolvimento. Logs contêm apenas messageId, template, resultado e motivo sanitizado, nunca corpo, destinatário ou token. O consumidor também verifica a validade, além do TTL do broker. A fila contém dados sensíveis temporariamente; esta entrega é local, e implantação com TLS e proteção de armazenamento terá spec própria.
11. **Operação:** logs no Seq, como o auth-service (ver `docs/project/0003 - Logging e Erros.md`). Sem endpoint HTTP de health check e sem `healthcheck` no compose nesta entrega; a falha é vista pelo log e pelo reinício do container (`restart: unless-stopped`). Encerramento gracioso: ao parar, não confirma a mensagem em andamento, que volta à fila.
12. **Fora de escopo:** SMS, push, anexos, preferências de notificação, API de templates, provedor externo escolhido, múltiplas réplicas e publicação da imagem do novo serviço. A CI deve compilar e testar o worker e suas integrações (RabbitMQ e SMTP de teste via Testcontainers, como o SQL Server no auth).

A spec pode ser testada com uma mensagem de exemplo antes de integrar o auth. As specs 2026101202 e 2026101203 usam este contrato.
