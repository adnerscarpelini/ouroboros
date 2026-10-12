# 2026101201 - Envio de e-mail por mensageria

**Data:** 12/10/2026
**Servico(s):** novo serviço: notification-service; infraestrutura Docker

## Solicitação

Criar as specs iniciais de uma solução simples para enviar os e-mails do auth-service por mensageria, com um serviço separado responsável pelo envio.

## Análise

O auth-service já gera e valida tokens, mas não envia e-mail. O novo notification-service cuida apenas de consumir solicitações e enviá-las por SMTP; não conhece usuários, não valida tokens e não acessa o banco do auth. Referências: [confirmações do RabbitMQ](https://www.rabbitmq.com/docs/confirms) e [Mailpit](https://mailpit.axllent.org/docs/).

Decisões:
1. **RabbitMQ no Docker**, com volume persistente, fila durável `notification.email.v1` e mensagens persistentes. Usar o exchange padrão, com routing key igual ao nome da fila. Credenciais por configuração, fora do Git; acesso local às interfaces de administração. O consumidor declara a fila; o publicador também a declara para funcionar sem depender da ordem de subida.
2. **notification-service como worker .NET**, sem API HTTP e sem banco. Manter Domain, Application e Infrastructure; usar `Notification.Worker` como projeto de entrada no lugar de Api. Essa adaptação da convenção de quatro projetos é intencional: o serviço recebe mensagens, não requisições HTTP. Nenhum ProjectReference entre serviços, nem biblioteca compartilhada de contratos nesta entrega.
3. **Contrato JSON v1:** `messageId` (GUID), `template` (`EmailConfirmation` ou `PasswordReset`), `recipientEmail`, `recipientName`, `token` e `expiresAt` (UTC). Uma solicitação envia um e-mail a um destinatário. O mesmo messageId é preservado nas republicações. Os produtores e o consumidor mantêm seus próprios DTOs compatíveis com esse contrato.
4. **Dois templates fixos**, pertencentes ao notification-service: confirmação de cadastro e recuperação de senha. Enviar assunto e texto simples com nome, token e validade. Não receber HTML, assunto ou remetente arbitrários na mensagem. Remetente configurado no serviço. Como não existe frontend, esta primeira entrega usa o token recebido nas APIs existentes, via Postman; links e telas ficam para uma spec posterior.
5. **SMTP configurável e Mailpit local**, ambos no compose. Mailpit captura os e-mails para consulta no navegador, sem envio a destinatários reais. Host, porta, remetente, TLS e credenciais SMTP ficam na configuração; startup valida valores obrigatórios. O envio tem timeout de 30 segundos.
6. **Um consumidor, uma mensagem por vez**, com prefetch 1 e confirmação manual somente após o servidor SMTP aceitar o envio. Isso indica aceitação pelo SMTP, não entrega na caixa do destinatário.
7. **Falha no SMTP:** aguardar 10 segundos e devolver a mensagem à fila. Repetir enquanto não expirar. Após expirar, confirmar e descartar sem enviar. Mensagem inválida (JSON, destinatário, template, token ou validade) é rejeitada sem requeue e descartada com log do motivo. Não haverá fila de falhas, política sofisticada de retry ou painel de reprocessamento nesta entrega. Perda de conexão com o broker deve permitir reconexão e retomada do consumo.
8. **Duplicidade aceita nesta versão.** Se o SMTP aceitar e o worker cair antes do ack, ou se o produtor republicar, o mesmo e-mail pode chegar novamente. Não criar inbox, tabela de deduplicação ou promessa de envio exatamente uma vez. O token mantém seu uso único no auth.
9. **Proteção mínima:** credenciais do broker separadas por serviço e permissões restritas à fila necessária; nenhum endpoint público de envio. Logs contêm apenas messageId, template, resultado e motivo sanitizado, nunca corpo, destinatário ou token. Definir expiração da mensagem no broker conforme expiresAt; o consumidor também verifica a validade. A fila contém dados sensíveis temporariamente; esta entrega é local, e implantação com TLS e proteção de armazenamento terá spec própria.
10. **Fora de escopo:** SMS, push, anexos, preferências de notificação, API de templates, provedor externo escolhido, múltiplas réplicas e publicação da imagem do novo serviço. A CI deve compilar e testar o worker e suas integrações.

A spec pode ser testada com uma mensagem de exemplo antes de integrar o auth. As specs 2026101202 e 2026101203 usam este contrato.
