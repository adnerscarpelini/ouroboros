# 0004 - SQL explícito e persistência

Este documento define o padrão de persistência do Ouroboros: SQL escrito manualmente e executado diretamente com Npgsql. O objetivo é que o SQL, os parâmetros, a transação e o mapeamento sejam visíveis e controláveis pelo time.

## Decisão

Toda leitura e escrita usa **SQL explícito via Npgsql**, com comandos parametrizados, transações explícitas e mapeamento manual das linhas retornadas.

## Organização

- Contratos ficam em `Application`, com parâmetros tipados e `CancellationToken`.
- Implementações ficam em `Infrastructure`, em `Repositories/` para agregados ou `Queries/` para leituras dedicadas.
- O SQL fica próximo do código que o utiliza, em constantes ou arquivos claramente associados ao repositório.
- O schema do próprio serviço deve aparecer explicitamente no SQL.

Exemplo:

```text
Application/Queries/IUserLoginAttemptsReportQuery.cs
Infrastructure/Queries/UserLoginAttemptsReportQuery.cs
```

## Execução

Npgsql será usado diretamente através de uma fábrica de conexões e uma sessão transacional:

- `IDbConnectionFactory` cria conexões do banco do serviço;
- `DbSession` mantém conexão e transação quando o caso de uso exige atomicidade;
- `DbCommand` recebe SQL parametrizado e `CancellationToken`;
- cada comando usa parâmetros, nunca concatenação de valores;
- o mapeamento de `DbDataReader` para entidade ou DTO é explícito.

## Tipos de retorno

Métodos de persistência retornam entidades de domínio, DTOs ou `record`s de leitura. Não usar `DataTable`, `DataSet`, `object` ou `byte[]` para transportar registros.

## Transações

Operações que fazem parte do mesmo caso de uso compartilham a mesma `DbTransaction`. A gravação de um dado de negócio e sua mensagem de outbox deve ocorrer na mesma transação.

## O que não usamos

- qualquer ORM para executar consultas ou comandos;
- micro-ORMs como camada intermediária;
- stored procedures como camada obrigatória de persistência;
- classes universais com estado mutável de parâmetros;
- tradução de exceções de banco para HTTP/WCF dentro da infraestrutura.

## Consequências

- O SQL é revisado como código de produção e recebe testes de integração contra PostgreSQL.
- O código precisa tratar explicitamente parâmetros, `NULL`, transações, concorrência e mapeamento de linhas.
- Não existe change tracking automático; alterações são comandos SQL intencionais.
- Uma única estratégia reduz a quantidade de abstrações e torna o comportamento do banco previsível.
