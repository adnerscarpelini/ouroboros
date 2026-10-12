# 2026101201 - Envio de e-mail por mensageria — Tarefas

- [ ] **Dev** — Criar notification-service com Domain, Application, Infrastructure e Worker; caso de uso de envio, dois templates de texto e gateway SMTP, sem referências ao auth
- [ ] **Dev** — Implementar consumidor RabbitMQ com contrato v1, validação, ack após aceitação SMTP, prefetch 1, timeout, retry simples, descarte de expiradas e reconexão
- [ ] **Dev** — Criar Dockerfile do worker e adicionar notification-service, RabbitMQ e Mailpit ao compose, volume do broker, credenciais separadas e configuração de SMTP/broker no .env.example; incluir projetos na solution e build/test na CI
- [ ] **Tester** — Verificar os dois templates no Mailpit, compatibilidade do JSON, descarte de inválidas/expiradas, retomada após falha SMTP e reinício do consumidor/broker com mensagens pendentes; garantir ausência de dados sensíveis nos logs
- [ ] **Tech Writer** — Documentar contrato v1, configuração e operação local, duplicidade aceita e ausência de frontend; atualizar arquitetura para a entrada Worker e registrar que não há banco no notification-service
