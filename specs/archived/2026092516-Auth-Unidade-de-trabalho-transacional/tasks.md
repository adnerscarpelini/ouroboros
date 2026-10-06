# 2026092516 - Unidade de trabalho transacional — Tarefas

- [x] **Dev** — Criar `IUnitOfWork` em `Auth.Application/Gateways` com `ExecuteAsync` (com e sem retorno) e registrar a implementação no `UseCaseConfiguration`
- [x] **DBA** — Criar em `Auth.Infrastructure/Persistence` a fábrica de conexões (singleton), o `DbSession` (scoped: conexão sob demanda e transação corrente) e a implementação `SqlUnitOfWork`, com rollback em exceção e erro em transação aninhada
- [x] **DBA** — Migrar `DapperUserRepository`, `DapperTokenRepository` e `DapperRefreshTokenRepository` para receber `DbSession` em vez da connection string, sem mudar o SQL
- [x] **Tester** — Criar um `FakeUnitOfWork` escrito à mão para os testes de Application, que executa o delegate e registra se houve commit
- [x] **Tester** — Integração (2026092512): commit persiste as duas escritas; exceção no meio desfaz as duas; transação aninhada lança erro; comandos fora da unidade de trabalho continuam em autocommit
- [x] **Tech Writer** — Adicionar a `docs/project/0001 - Arquitetura.md` uma seção sobre transações: `IUnitOfWork`, `DbSession`, quando usar e o que fica fora da transação
