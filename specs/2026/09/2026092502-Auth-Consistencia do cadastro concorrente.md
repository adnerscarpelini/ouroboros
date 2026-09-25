# 2026092502 - Consistencia do cadastro concorrente

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

O usuário aprovou especificar a consistência do cadastro e o tratamento de requisições simultâneas que disputam login ou e-mail.

## Análise

O `RegisterUserInteractor` remove cadastros abandonados, insere usuário e cria token de confirmação em escritas separadas. A checagem prévia de unicidade também pode perder uma corrida para o índice único do PostgreSQL e hoje resultar em `500`. Manter as decisões da spec 2026092305: e-mail ocupado recebe resposta genérica e login ocupado pode ser informado. A identidade comparada seguirá a política da spec 2026092508. O fluxo continua público, com limite de requisições, cadastro sempre `User` e sem expor token ou dados da conta na resposta.

## Tarefas

- [ ] **Dev** — Executar remoção de cadastro abandonado, criação do usuário e token de confirmação como uma unidade; preservar o contrato público `202` para e-mail ocupado.
- [ ] **DBA** — Implementar transação no gateway sem levar detalhes de Npgsql para Domain/Application; traduzir conflitos de unicidade com base na restrição violada, sem expor SQL ou existência de e-mail.
- [ ] **Tester** — Cobrir rollback em cada escrita e cadastros concorrentes com mesmo login ou e-mail usando PostgreSQL real, além das regras de ownership do cadastro abandonado.
- [ ] **Tech Writer** — Atualizar `docs/auth/0001 - Confirmacao de Cadastro.md` e a collection Postman se o contrato de erro de login mudar.
