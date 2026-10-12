# 2026101202 - E-mail de confirmação com outbox — Tarefas

- [ ] **DBA** — Criar migration de auth.email_outbox com índice de leitura e implementar inserção, leitura de pendências e remoção via Dapper/DbSession, respeitando as transações e permissões existentes
- [ ] **Dev** — Adicionar gateway de outbox na Application e gravar a solicitação EmailConfirmation na transação do cadastro; remover token do resultado interno e o log provisório do controller
- [ ] **Dev** — Implementar publicador em background, configuração, confirmação/mandatory, TTL, nova tentativa no próximo ciclo e remoção de publicadas/expiradas, sem manter transação durante acesso ao broker
- [ ] **Tester** — Provar rollback de usuário/token/auditoria/outbox, ausência de solicitação para e-mail ocupado e continuidade do cadastro com broker indisponível; validar publicação posterior, retenção em falha e remoção após confirmação ou expiração
- [ ] **Tester** — Executar cadastro → e-mail no Mailpit → confirmação → login; atualizar unitários e integração para o resultado sem token e garantir que nenhum log exponha o segredo
- [ ] **Tech Writer** — Atualizar confirmação de cadastro, Docker e configuração; documentar outbox, publicador único, dados sensíveis temporários e duplicidade possível
