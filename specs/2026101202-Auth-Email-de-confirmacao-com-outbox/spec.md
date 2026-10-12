# 2026101202 - E-mail de confirmação com outbox

**Data:** 12/10/2026
**Servico(s):** auth-service

## Solicitação

Completar o cadastro para que o token de confirmação chegue por e-mail, usando a solução simples de mensageria e o serviço de notificações.

## Análise

Extensão do cadastro existente, dependente da spec 2026101201. A decisão de criar tokens e sua validação permanece no auth. A única persistência adicional é a outbox no banco do próprio serviço. Referência: [Transactional Outbox](https://microservices.io/patterns/data/transactional-outbox).

Decisões:
1. **Na mesma IUnitOfWork do cadastro**, gravar usuário, hash do token, auditoria existente e solicitação de e-mail na outbox. Se qualquer escrita falhar, tudo sofre rollback. Não chamar RabbitMQ ou SMTP dentro dessa transação.
2. **Tabela `auth.email_outbox`** com id GUID (messageId), payload JSON do contrato v1, created_at e expires_at. Não criar histórico de envios, status intermediários ou contadores nesta primeira versão. Índice para leitura por created_at. O payload precisa conter o token em claro para envio; isso é separado da tabela auth.tokens, que continua guardando apenas o hash.
3. **Publicador em background no auth**, em uma única instância, consulta até 20 pendências a cada 5 segundos e publica uma por vez em `notification.email.v1`. Usar mensagem persistente, publisher confirms e mandatory para identificar mensagens sem destino. Remover a linha somente após confirmação positiva e ausência de retorno por falta de rota. Em falha ou resultado incerto, manter e tentar no ciclo seguinte. Nenhuma transação de banco fica aberta aguardando o broker.
4. **Sem coordenação entre réplicas nesta entrega.** Há um publicador ativo; se o auth ganhar múltiplas instâncias, o claim das pendências deverá entrar em outra spec. Falha após publicação e antes da remoção pode repetir o e-mail, conforme a limitação aceita na spec 2026101201.
5. **Expiração e remoção:** não publicar solicitações vencidas; removê-las a cada ciclo. Publicar com TTL equivalente ao tempo restante até expiresAt. Apagar publicadas evita manter cópias desnecessárias do token em claro. Não logar o payload, o token ou o e-mail, inclusive em erros.
6. **Cadastro com e-mail ocupado continua sem envio e com a mesma resposta genérica.** Cadastro abandonado, conflitos concorrentes, validade de 24h, rate limits, auditoria e consumo único do token mantêm as regras existentes. Aviso de tentativa de cadastro ao dono do e-mail fica fora de escopo.
7. **Remover EmailConfirmationToken de RegisterUserResponse e o log temporário do controller.** A resposta HTTP continua 202 com a mensagem atual. O destino da notificação usa o e-mail validado da entidade criada.
8. **Broker indisponível não impede cadastro** quando o banco está disponível: a solicitação fica pendente. Falha de gravação da outbox, por outro lado, impede a confirmação da transação de cadastro.
9. **Configuração do publicador:** conexão/credenciais do broker e opção Enabled, desabilitada nos testes que não exercitam a publicação. Validar configuração no startup quando habilitado. Intervalo e lote são constantes nesta entrega. Secrets fora do Git.
10. **Entrega local e testável:** receber no Mailpit, copiar o token para POST /api/users/confirm-email e concluir login. Nenhuma tela ou endpoint novo de reenvio de confirmação será criado.
