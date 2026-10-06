# 2026092503 - Confirmacao atomica de cadastro — Tarefas

- [x] **Dev** — Executar o consumo do token e o `ConfirmEmail()` do usuário dentro do `IUnitOfWork`, usando `TryMarkAsUsedAsync`, e devolver a mensagem genérica única para qualquer token inválido
- [x] **DBA** — Incluir `expires_at > @now` na condição do `TryMarkAsUsedAsync` do `DapperTokenRepository` (compartilhado com 2026092504)
- [x] **Tester** — Unitários: token expirado, já usado, de outro tipo e de conta excluída → mesma mensagem. Integração: duas confirmações simultâneas → uma `200` e uma `400`; falha forçada na atualização do usuário → token continua pendente
- [x] **Tech Writer** — Atualizar `docs/auth/0001 - Confirmacao de Cadastro.md` com a garantia de uso único e a mensagem genérica
