# 2026092509 - Politica e armazenamento de senhas

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar atualização da política de senha e do custo do hash para os padrões atuais.

## Análise

O auth-service usa senha como fator único, aceita oito caracteres com regras de composição e grava PBKDF2-HMAC-SHA256 com 100 mil iterações. Seguir NIST SP 800-63B: exigir ao menos 15 caracteres para novas senhas, permitir frases e ao menos 64 caracteres, retirar regras de composição e recusar senhas comuns/comprometidas por lista segura. Seguir OWASP Password Storage Cheat Sheet: elevar PBKDF2-HMAC-SHA256 para 600 mil iterações, medindo custo no ambiente alvo. Hashes antigos continuam verificáveis e são atualizados após autenticação bem-sucedida; senhas legadas não deixam de funcionar por mudança de política. Limitar tamanho de entrada para conter custo excessivo sem registrar a senha.

## Tarefas

- [ ] **Dev** — Aplicar a nova política a cadastro e reset, incluir bloqueio de senhas comuns/comprometidas sem enviar senha em claro a serviço externo e ajustar mensagens de validação.
- [ ] **Dev** — Aumentar o custo de novos hashes e atualizar hashes antigos no login bem-sucedido, preservando compatibilidade com o formato já gravado.
- [ ] **DBA** — Garantir atualização concorrente segura do hash legado, sem sobrescrever troca de senha ocorrida em paralelo.
- [ ] **Tester** — Cobrir frases longas, caracteres aceitos, lista bloqueada, verificação de hash antigo e atualização gradual; medir custo do hash para evitar sobrecarga do login.
- [ ] **Tech Writer** — Atualizar `docs/auth/0001 - Confirmacao de Cadastro.md` e `docs/auth/0004 - Recuperacao de Senha.md` com a nova política.
