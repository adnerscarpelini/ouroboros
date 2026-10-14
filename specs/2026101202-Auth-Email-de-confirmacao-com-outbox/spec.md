# 2026101202 - E-mail de confirmação com outbox

**Data:** 12/10/2026
**Servico(s):** auth-service

## Solicitação

Completar o cadastro para que o token de confirmação chegue por e-mail, usando a solução simples de mensageria e o serviço de notificações.

## Análise

Extensão do cadastro existente, dependente da spec 2026101201. A decisão de criar tokens e sua validação permanece no auth. A única persistência adicional é a outbox no banco do próprio serviço. Referência: [Transactional Outbox](https://microservices.io/patterns/data/transactional-outbox).

Decisões:
1. **Na mesma IUnitOfWork do cadastro**, gravar usuário, hash do token, auditoria existente (`UserRegistered`) e solicitação de e-mail na outbox. Se qualquer escrita falhar, tudo sofre rollback. Não chamar RabbitMQ ou SMTP dentro dessa transação.
2. **Tabela `auth.email_outbox`**, no padrão de entidade persistida do projeto: `id` bigint identity (chave clusterizada e ordem de leitura), `external_id` (o messageId do contrato v1), payload JSON do contrato v1, `expires_at`, `created_at` e `updated_at`. Sem FK para `auth.users`: a solicitação independe da conta depois de gravada. Não criar histórico de envios, status intermediários ou contadores nesta primeira versão. O payload precisa conter o token em claro para envio; isso é separado da tabela auth.tokens, que continua guardando apenas o hash. O papel `auth_service` já tem DML no schema `auth`, sem GRANT novo.
3. **Publicador em background no auth** (`BackgroundService`, como o `ExpiredTokenCleanupService`), em uma única instância, lê até 20 pendências por ordem de `id` a cada 5 segundos e publica uma por vez em `notification.email.v1`, com as propriedades AMQP da spec 2026101201. Usar publisher confirms e mandatory para identificar mensagens sem destino (inclusive fila ainda não provisionada). Remover a linha somente após confirmação positiva e ausência de retorno por falta de rota. Em falha ou resultado incerto, manter e tentar no ciclo seguinte. Nenhuma transação de banco fica aberta aguardando o broker. A lógica do ciclo fica num caso de uso da Application, com gateways de outbox e de publicação; RabbitMQ só na Infrastructure.
4. **Sem coordenação entre réplicas nesta entrega.** Há um publicador ativo; se o auth ganhar múltiplas instâncias, o claim das pendências deverá entrar em outra spec. Falha após publicação e antes da remoção pode repetir o e-mail, conforme a limitação aceita na spec 2026101201.
5. **Expiração e remoção:** não publicar solicitações vencidas; removê-las a cada ciclo. Publicar com TTL equivalente ao tempo restante até expiresAt. Apagar publicadas evita manter cópias desnecessárias do token em claro. Não logar o payload, o token ou o e-mail, inclusive em erros. Se o broker ficar fora por mais que a validade do token, o e-mail não sai; vale a regra de cadastro abandonado (novo cadastro depois de 24h).
6. **Cadastro com e-mail ocupado continua sem envio e com a mesma resposta genérica.** Cadastro abandonado, conflitos concorrentes, validade de 24h, rate limits, auditoria e consumo único do token mantêm as regras existentes. Aviso de tentativa de cadastro ao dono do e-mail fica fora de escopo; o `TODO` correspondente no `RegisterUserInteractor` continua.
7. **Remover EmailConfirmationToken de RegisterUserResponse e o log do token no controller.** `UserId` continua no resultado interno: o controller usa `UserId` nulo para registrar o `Warning` de e-mail já usado, que hoje depende do token nulo. A resposta HTTP continua 202 com a mensagem atual (que fala em "link"; a troca da mensagem fica para a spec que criar o link). O destino da notificação usa o e-mail e o nome da entidade criada.
8. **Broker indisponível não impede cadastro** quando o banco está disponível: a solicitação fica pendente. Falha de gravação da outbox, por outro lado, impede a confirmação da transação de cadastro. O broker não entra no `/health/ready` do auth.
9. **Configuração do publicador:** conexão/credenciais do broker e opção Enabled, desabilitada nos testes que não exercitam a publicação. Validar configuração no startup quando habilitado (`ValidateOnStart`). Intervalo e lote são constantes nesta entrega. Secrets fora do Git. No compose, o auth-service não depende do broker para subir.
10. **Entrega local e testável:** receber no Mailpit, copiar o token para POST /api/users/confirm-email e concluir login. Nenhuma tela ou endpoint novo de reenvio de confirmação será criado.
11. **Diferença de tempo continua.** A doc atual diz que a mensageria reduziria a diferença de tempo entre e-mail novo e e-mail já usado. Com outbox, o cadastro de e-mail novo grava uma linha a mais, de forma síncrona; a limitação permanece e a doc deve ser corrigida.
