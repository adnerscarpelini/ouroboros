# 2026101203 - E-mail de recuperação de senha — Tarefas

- [ ] **Dev** — Inserir solicitação PasswordReset na outbox dentro da transação existente, reutilizando contrato e publicador da spec 2026101202; remover token do resultado interno e o log do token no controller
- [ ] **Tester** — Provar rollback do novo token/invalidação/auditoria/outbox; verificar que contas inexistentes/inativas/não confirmadas não produzem mensagens e mantêm a resposta genérica
- [ ] **Tester** — Executar solicitação → Mailpit → redefinição → login; testar duas solicitações, validade exclusiva do último token, revogação de sessões e ausência de tokens nos logs
- [ ] **Tech Writer** — Atualizar recuperação de senha (remover aviso de token no log, corrigir a limitação de diferença de tempo) e o roteiro Postman para envio real ao Mailpit, incluindo a possibilidade de e-mails antigos chegarem depois do mais recente
