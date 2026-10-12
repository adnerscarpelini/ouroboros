# 2026101203 - E-mail de recuperação de senha

**Data:** 12/10/2026
**Servico(s):** auth-service

## Solicitação

Completar a recuperação de senha para que o usuário receba o token por e-mail, reutilizando a mensageria e o serviço de notificações.

## Análise

Extensão do fluxo existente. Depende das specs 2026101201 e 2026101202 e reutiliza contrato, fila, outbox e publicador; não introduz outro serviço ou tabela.

Decisões:
1. **Na mesma IUnitOfWork da solicitação**, invalidar tokens anteriores, gravar o hash do novo token, registrar a auditoria e inserir a solicitação PasswordReset na outbox. Falha em qualquer escrita desfaz todas essas alterações.
2. **Enviar para o e-mail da conta encontrada**, nunca para um endereço arbitrário fornecido como destino pelo cliente. Nome, token e expiresAt entram no contrato v1; validade permanece de 1h.
3. **Usuário inexistente, inativo ou sem confirmação de e-mail:** não gerar token nem mensagem; manter a resposta genérica 202, validações e rate limit existentes. Nenhum novo endpoint.
4. **Remover PasswordResetToken de RequestPasswordResetResponse e o log temporário do controller.** O token passa a sair do auth apenas pelo payload da notificação; hash em auth.tokens e sigilo nos logs seguem as decisões da spec 2026101202.
5. **Só o token mais recente é válido**, conforme a regra atual. E-mails anteriores podem já estar na fila ou chegar fora de ordem. Informar no template que uma nova solicitação invalida os códigos anteriores; não criar cancelamento distribuído de mensagens nem consulta do notification-service ao auth.
6. **Reutilizar publicação, TTL, retry e descarte de expiradas.** Broker indisponível mantém a solicitação no banco. Duplicidade de envio é aceita como na spec 2026101201; o consumo do token continua único.
7. **Teste completo local:** receber o token no Mailpit, chamar POST /api/users/password-reset/confirm com uma nova senha e fazer login. Preservar desbloqueio da conta, revogação de sessões e demais garantias do reset existente.
8. **Fora de escopo:** notificação de senha alterada, frontend, links de recuperação e mudança das políticas de senha. Podem ser adicionados separadamente quando houver necessidade.
