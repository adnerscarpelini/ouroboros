# 2026092501 - Protecao contra tentativas de autenticacao

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar a proteção contra tentativas de login e outros usos abusivos dos endpoints de autenticação, além do envio de e-mails.

## Análise

O `auth-service` já limita cadastro, solicitação e confirmação de reset e exclusão por IP, mas não limita login, refresh nem confirmação de e-mail. Seguir OWASP Authentication Cheat Sheet: combinar limite por origem com controle de falhas por conta no login, sem bloqueio permanente que permita negar serviço à vítima. Endpoints continuam públicos porque recebem credenciais ou tokens; respostas de autenticação não devem revelar a existência da conta. O limite precisa funcionar com mais de uma réplica; a identificação da origem depende da configuração de proxy da spec 2026092511.

## Tarefas

- [ ] **Dev** — Aplicar políticas de limite a login, refresh e confirmação de e-mail; definir resposta `429` e evitar registrar senhas ou tokens nos eventos de rejeição.
- [ ] **Dev** — Contabilizar falhas de senha por login normalizado, com janela e recuperação automáticas; manter erro genérico para login inexistente, senha incorreta e limite por conta.
- [ ] **DBA** — Definir persistência compartilhada dos contadores, se necessária para garantir os limites entre réplicas, com expiração e sem armazenar credenciais.
- [ ] **Tester** — Cobrir limites por origem e conta, sucesso após expiração da janela, ausência de bloqueio permanente e comportamento com contas inexistentes.
- [ ] **Tech Writer** — Atualizar os contratos, limites e comportamento de rejeição em `docs/auth/` e na collection Postman.
