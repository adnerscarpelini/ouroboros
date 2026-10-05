# 2026092516 - Unidade de trabalho transacional

**Data:** 25/09/2026
**Status:** Em andamento
**Servico(s):** auth-service

## Solicitação

Na revisão das specs `20260925*`, ficou claro que as specs 2026092502 a 2026092507 pediam, cada uma, "implementar transação no gateway" sem definir como. O usuário aprovou criar uma spec base que decide o mecanismo único de transação, da qual as demais dependem.

## Análise

Extensão do `auth-service`, sem serviço novo. Hoje cada método dos repositórios Dapper abre a própria `SqlConnection` a partir da connection string. Por isso não há como juntar duas escritas numa transação. Referências: padrão Unit of Work (Fowler, PoEAA), o mesmo papel do `DbContext` + `SaveChanges` do EF Core, e o uso de uma fábrica de conexões singleton.

Decisões:
1. **Gateway na Application: `IUnitOfWork`** com `Task<T> ExecuteAsync<T>(Func<Task<T>> work)` e uma sobrecarga sem retorno. O método abre a transação, executa o trabalho e faz o commit. Qualquer exceção provoca rollback e é relançada.
   - Não existem `Begin`/`Commit` soltos, então ninguém consegue esquecer o commit nem deixar uma transação aberta.
2. **Infrastructure:**
   - Uma fábrica de conexões `SqlConnection` (singleton) substitui a connection string solta.
   - Um `DbSession` scoped (um por request) guarda uma conexão aberta sob demanda e a transação corrente.
   - Os repositórios passam a receber o `DbSession` e informam a `Transaction` ao Dapper.
   - Fora de uma unidade de trabalho, cada comando roda em autocommit, como hoje.
3. **Sem transação aninhada.** Chamar `ExecuteAsync` dentro de outro `ExecuteAsync` lança `InvalidOperationException`. É erro de programação e deve aparecer cedo.
4. **Isolamento Read Committed**, o padrão do SQL Server. Onde a regra de negócio precisar de mais, cada spec decide um lock explícito (`WITH (UPDLOCK, HOLDLOCK)`, spec 2026092505) ou uma escrita condicional (`WHERE used_at IS NULL`, specs 2026092503, 2026092504 e 2026092507).
5. **Nada sensível sai antes do commit.** O interactor só devolve tokens depois que o `ExecuteAsync` termina com sucesso.
6. **Escritas que precisam sobreviver a um rollback rodam fora da transação principal:**
   - o contador de falhas de senha (2026092501);
   - a revogação da sessão quando há reuso de refresh token (2026092507);
   - os eventos de auditoria de falha (2026092519).
7. **`Microsoft.Data.SqlClient` não aparece em Domain nem em Application.** A regra de dependência da Clean Architecture continua valendo.

Ordem de implementação: depois de 2026092512 (testes de integração), que é onde commit e rollback são provados de verdade. Esta spec vem antes de 2026092502 a 2026092507.

## Tarefas

- [ ] **Dev** — Criar `IUnitOfWork` em `Auth.Application/Gateways` com `ExecuteAsync` (com e sem retorno) e registrar a implementação no `UseCaseConfiguration`
- [ ] **DBA** — Criar em `Auth.Infrastructure/Persistence` a fábrica de conexões (singleton), o `DbSession` (scoped: conexão sob demanda e transação corrente) e a implementação `SqlUnitOfWork`, com rollback em exceção e erro em transação aninhada
- [ ] **DBA** — Migrar `DapperUserRepository`, `DapperTokenRepository` e `DapperRefreshTokenRepository` para receber `DbSession` em vez da connection string, sem mudar o SQL
- [ ] **Tester** — Criar um `FakeUnitOfWork` escrito à mão para os testes de Application, que executa o delegate e registra se houve commit
- [ ] **Tester** — Integração (2026092512): commit persiste as duas escritas; exceção no meio desfaz as duas; transação aninhada lança erro; comandos fora da unidade de trabalho continuam em autocommit
- [ ] **Tech Writer** — Adicionar a `docs/project/0001 - Arquitetura.md` uma seção sobre transações: `IUnitOfWork`, `DbSession`, quando usar e o que fica fora da transação
