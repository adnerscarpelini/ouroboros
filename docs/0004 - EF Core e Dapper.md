# 0004 - EF Core e Dapper

## Contexto

O EF Core é ótimo pro lado de escrita (comandos, agregados, invariantes de domínio) e pra leituras simples/moderadas. Mas pra relatórios e consultas pesadas — múltiplos joins, agregações, window functions — o LINQ tende a gerar SQL ruim e fica difícil de ajustar. Esse documento define quando sair do EF Core pra leitura e como isso fica estruturado sem quebrar a Clean Architecture.

## Decisão

Regra geral: **EF Core continua sendo o padrão pra tudo** — comandos e a maioria das leituras. Só se cria um **Query Object** com **Dapper** quando uma leitura específica for realmente pesada (relatório/dashboard com múltiplos joins e agregações, ou uma consulta que já ficou difícil/ineficiente em LINQ) — nunca por antecipação, só quando existir a necessidade real.

### Query Object

- Contrato mora em `Application`, um por consulta: interface `I<Nome>Query` com um único método `ExecuteAsync(...)`, recebendo os parâmetros da consulta e `CancellationToken`, retornando um DTO (`record`) dedicado — nunca uma entidade de `Domain`. Isso mantém o lado de leitura desacoplado das invariantes de escrita (é um CQRS leve, só na camada de dados, não uma reescrita da arquitetura).
- Implementação mora em `Infrastructure`, pasta `Queries/`, usando Dapper com SQL escrito à mão, contra o schema do próprio serviço (schema explícito na query, já que Dapper não conhece o `HasDefaultSchema` configurado no `DbContext`).
- Registro no mesmo `Add<NomeDoServico>Module` onde hoje entram os serviços, como `services.AddScoped<I<Nome>Query, <Nome>Query>();`.

Exemplo (nomes ilustrativos — não existe ainda no código):

```
Application/Queries/IUserLoginAttemptsReportQuery.cs   → interface + record de resultado
Infrastructure/Queries/UserLoginAttemptsReportQuery.cs → implementação com Dapper
```

### Conexão

Dapper precisa de uma `IDbConnection` própria, fora do `DbContext`. Um `IDbConnectionFactory` (contrato em `Ouroboros.BuildingBlocks.Application`, implementação Npgsql em `Ouroboros.BuildingBlocks.Infrastructure`) fica disponível pra qualquer serviço pedir uma conexão pra Dapper, reaproveitando a mesma connection string já usada pelo EF Core — sem duplicar configuração ou segredo.

### O que não muda

- Escrita (criar, alterar, apagar) continua sempre por EF Core — agregados, `Entity`, invariantes de domínio, change tracking.
- Um Query Object nunca escreve no banco.
- O isolamento entre serviços continua valendo: um Query Object só lê do banco do próprio serviço.

## Consequências

- Duas formas de acessar o banco convivem por serviço quando necessário (EF Core pra escrita, Dapper pra leitura pesada) — mais uma peça pra manter, só onde compensar.
- SQL escrito à mão nos Query Objects não se beneficia da checagem em tempo de compilação que o LINQ dá — revisão manual fica mais importante ali.
- Resultado de Query Object é sempre um DTO, nunca uma entidade rastreada — evita a tentação de consultar com Dapper e depois dar `SaveChanges` na mesma instância.
- `BuildingBlocks.Infrastructure` ganha uma peça de infraestrutura genérica (`IDbConnectionFactory`) só quando o primeiro serviço realmente precisar dela — não é criada agora, por antecipação.
