# 2026101202 - E-mail de confirmação com outbox — Tarefas

- [ ] **DBA** — Criar migration de auth.email_outbox no padrão de entidade persistida (id, external_id, auditoria) e implementar inserção, leitura de pendências por id e remoção (publicadas e expiradas) via Dapper/DbSession, respeitando as transações e permissões existentes
- [ ] **Dev** — Adicionar gateway de outbox na Application e gravar a solicitação EmailConfirmation na transação do cadastro; remover o token do resultado interno e o log do token no controller, mantendo o Warning de e-mail já usado pelo UserId nulo
- [ ] **Dev** — Implementar caso de uso e BackgroundService do publicador, gateway RabbitMQ, configuração validada no startup, confirmação/mandatory, TTL, nova tentativa no próximo ciclo e remoção de publicadas/expiradas, sem manter transação durante acesso ao broker; ajustar compose e .env.example
- [ ] **Tester** — Provar rollback de usuário/token/auditoria/outbox, ausência de solicitação para e-mail ocupado e continuidade do cadastro com broker indisponível; validar publicação posterior, retenção em falha ou sem rota e remoção após confirmação ou expiração
- [ ] **Tester** — Executar cadastro → e-mail no Mailpit → confirmação → login; atualizar unitários e integração para o resultado sem token e garantir que nenhum log exponha o segredo
- [ ] **Tech Writer** — Atualizar confirmação de cadastro (remover aviso de token no log, corrigir a limitação de diferença de tempo), Docker e configuração; documentar outbox, publicador único, dados sensíveis temporários e duplicidade possível
