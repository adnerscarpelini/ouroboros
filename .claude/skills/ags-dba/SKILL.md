---
name: ags-dba
description: Convenções de banco de dados do projeto Ouroboros — PostgreSQL, schemas, migrations, nomenclatura de tabelas/colunas e boas práticas. Use sempre que for criar/alterar entidades persistidas, escrever migrations, ou tomar qualquer decisão relacionada a banco de dados neste projeto.
---

# ags-dba

## Specs como histórias de trabalho

- Antes de criar ou alterar persistência para uma nova feature, procure em `specs/` a spec correspondente e pergunte ao usuário se ela já existe.
- Se não existir, crie a spec antes da implementação; se existir, use-a como fonte de escopo e atualize-a durante o trabalho. Specs são histórias/items de trabalho no estilo Jira e usam `AAAAMMDDHHMMSS-Descricao.md`, com `title` e `state` (`new`, `in progress` ou `done`) no cabeçalho YAML. Specs históricas mantêm seus nomes originais.
- Ao sugerir a mensagem de commit de uma alteração de banco, inclua a data/número da spec correspondente.

Skill base para tudo relacionado a banco de dados no projeto Ouroboros. Complementa a [ags-developer](../ags-developer/SKILL.md) (convenções gerais de código), a [ags-qa](../ags-qa/SKILL.md) (testes) e a [ags-devops](../ags-devops/SKILL.md) — esta cuida do **container** que hospeda o Postgres, das portas e do Compose; aqui ficam banco, schema, migrations e nomenclatura.

## Banco de dados

- SGBD: **PostgreSQL**.
- Uma única instância Postgres compartilhada entre serviços — não um container por serviço. Ver [docs/0000 - Arquitetura.md](../../../docs/0000%20-%20Arquitetura.md#banco-de-dados).

## Banco e schema por serviço

- Cada serviço (`src/Services/<NomeDoServico>/`) tem seu próprio **banco lógico** na instância Postgres, nomeado `ouroboros_<nomedoservico>` (ex.: serviço `Auth` → banco `ouroboros_auth`), com uma *role* própria que é dona desse banco. É isso — banco físico, não só schema — que garante o isolamento entre serviços: nenhum serviço tem credencial que alcance o banco de outro.
- Dentro do próprio banco de um serviço, o schema organiza por assunto: `<nomedoservico>` pras tabelas de negócio (ex.: schema `auth`) e `common` pras tabelas técnicas vindas do `BuildingBlocks` (`ErrorLog` e `OutboxMessage`) — ver [docs/0000](../../../docs/0000%20-%20Arquitetura.md#buildingblocks) sobre código vs. dado compartilhado.
- Banco/role de cada serviço são criados por um script em `docker/postgres/init/` na primeira subida do container (ver [docs/0002](../../../docs/0002%20-%20Setup%20do%20Banco%20de%20Dados%20Local.md)) — nunca criados manualmente. O script também **revoga `CONNECT`/`TEMPORARY` de `PUBLIC`** no banco criado: o PostgreSQL concede conexão a `PUBLIC` por padrão, e sem isso a role de um serviço alcançaria o banco de outro — o isolamento precisa ser real, não convenção. O SQL desse script é decisão daqui; o arquivo, o container e o Compose seguem a [ags-devops](../ags-devops/SKILL.md). Scripts `.sh` rodam dentro de um container Linux e **precisam** de finais de linha LF — o `.gitattributes` da raiz garante isso.

## Migrations

- A estratégia adotada é **SQL-first**: migrations novas são arquivos `.sql` escritos manualmente, sem gerar o schema a partir de modelos de ORM.
- Cada serviço com persistência mantém suas migrations na própria camada `Infrastructure`, em `Migrations/`, e aplica somente migrations do seu próprio banco lógico.
- O nome de toda migration nova deve seguir exatamente:
  ```text
  AAAAMMDDHHMMSS-Descricao.sql
  ```
  O prefixo tem 14 dígitos, usando a data e hora local de criação da migration: ano, mês, dia, hora, minuto e segundo. A descrição usa PascalCase ou palavras separadas por hífen, sem espaços. Exemplos:
  ```text
  20260915143000-CreateUsersTable.sql
  20260915143112-AddEmailConfirmedAtToUsers.sql
  ```
- O prefixo temporal é a ordenação oficial. Duas migrations não podem ter o mesmo prefixo; se necessário, aguardar o próximo segundo ou ajustar manualmente o timestamp antes do commit.
- Depois que uma migration for aplicada em qualquer ambiente compartilhado, seu arquivo é imutável. Correções devem ser feitas em uma nova migration posterior; não reescrever o histórico.
- O executor deve manter uma tabela de histórico por banco, por exemplo `public.schema_migrations`, registrando o nome/versionamento, checksum e data de aplicação. Uma migration já registrada com checksum diferente deve interromper a execução.
- Cada migration deve ser transacional quando o PostgreSQL permitir. DDL destrutivo exige revisão explícita e, para mudanças incompatíveis, deve-se preferir o padrão expandir/migrar/contrair.
- Seeds e dados de referência pertencem a migrations SQL idempotentes ou versionadas, nunca a código de inicialização da API. A migration deve declarar o schema explicitamente e respeitar `snake_case`.
- Todas as migrations do projeto devem ser arquivos `.sql` manuais. O histórico legado que não estiver em SQL deve ser convertido para SQL antes de permanecer no repositório; não manter arquivos de migration de ORM.
- A aplicação das migrations será feita por uma ferramenta administrativa/runner própria do projeto, fora do startup normal da API. O Compose não aplica migrations automaticamente.
- Palavras-chave SQL em maiúsculo (`CREATE TABLE`, `ALTER TABLE`, `NOT NULL`, `PRIMARY KEY`, `CONSTRAINT`, `FOREIGN KEY`, `REFERENCES`, `ON DELETE CASCADE`, `GENERATED BY DEFAULT AS IDENTITY`, `CREATE UNIQUE INDEX`, etc.) — mesma convenção do SQL escrito nos repositórios (ver "Persistência com SQL explícito" abaixo). Nomes de schema/tabela/coluna continuam em `snake_case`; nomes de tipo (`bigint`, `uuid`, `text`, `timestamp with time zone`, `boolean`, `integer`) ficam em minúsculo, como identificadores.

## Registro do serviço (DI)

- Cada serviço com persistência expõe um único método de extensão em `Infrastructure`, no padrão `Add<NomeDoServico>Module(this IServiceCollection services, string connectionString, ...)`, que registra `IDbConnectionFactory`, `DbSession`, repositórios e serviços de `Application` daquele serviço. A `Api` do serviço só chama esse método — não registra repositórios/conexões diretamente no `Program.cs`.

## Segredos e connection string

- A connection string com a senha real **nunca** vai pro `appsettings.json` (esse arquivo é versionado). Local, ela fica no **User Secrets** do projeto `Api` de cada serviço (`dotnet user-secrets`), equivalente ao papel do `.env` no Docker Compose — ver [docs/0002 - Setup do Banco de Dados Local.md](../../../docs/0002%20-%20Setup%20do%20Banco%20de%20Dados%20Local.md).
- Rodando em container, a connection string chega por variável de ambiente (`ConnectionStrings__Postgres`), montada no `docker-compose.yml` a partir do `.env` — ver [ags-devops](../ags-devops/SKILL.md).

## Entidade base

Toda entidade persistida herda de `Entity` (`Ouroboros.BuildingBlocks.Domain`), que carrega quatro colunas presentes em **todas** as tabelas do sistema, sempre nessa ordem física (definida nas migrations SQL):

1. `id` (`long` / `bigint`, identity) — chave primária interna, usada em joins e FKs. Nunca exposta pela Api.
2. `external_id` (`Guid` / `uuid`, único, gerado em `Guid.NewGuid()` na criação) — identificador público, usado em rotas/DTOs da Api. Enumeration-safe: não revela volume nem ordem de criação como um `id` sequencial exposto revelaria.
3. `created_at` (`timestamptz`, UTC) — carimbado automaticamente na criação, dentro do construtor de `Entity`.
4. `updated_at` (`timestamptz`, UTC, nullable) — `null` até a primeira alteração; atualizado explicitamente pelo repositório no mesmo comando SQL que persiste a alteração.

```csharp
public abstract class Entity
{
	public long Id { get; private set; }
	public Guid ExternalId { get; private set; }
	public DateTime CreatedAt { get; private set; }
	public DateTime? UpdatedAt { get; private set; }

	protected Entity()
	{
		ExternalId = Guid.NewGuid();
		CreatedAt = DateTime.UtcNow;
	}

	public void MarkAsUpdated()
	{
		UpdatedAt = DateTime.UtcNow;
	}
}
```

Os índices, FKs, ordem das colunas e carimbos de atualização são definidos diretamente nas migrations SQL. Os repositórios devem atualizar `updated_at` de forma explícita quando alterarem uma entidade.

## Entidades persistidas (reidratação SQL)

Toda entidade persistida precisa, além do construtor público "de verdade" (com as regras de negócio) e de herdar de `Entity`, de uma fábrica `Rehydrate(...)` usada pelo mapper SQL:

- A fábrica deve receber os valores persistidos e restaurar `Id`, `ExternalId`, `CreatedAt` e `UpdatedAt` por meio de `Entity.RestorePersistence`.
- **`private set`** em toda propriedade de domínio, preservando invariantes fora do mapper.

```csharp
public sealed class ErrorLog : Entity
{
	public string Source { get; private set; } = null!;
	// ...

	private ErrorLog()
	{
	}

	public ErrorLog(string source, /* ... */)
	{
		Source = source;
		// ... (Id, ExternalId, CreatedAt já vêm do construtor de Entity)
	}
}
```

Propriedades de referência não-nulas (`string`, não `string?`) devem ser inicializadas no construtor público ou na fábrica de reidratação.

## Nomenclatura (casing)

- Tabelas e colunas no Postgres: **snake_case** (ex.: tabela `users`, coluna `created_at`), seguindo a convenção idiomática do Postgres.
- Entidades, repositórios e propriedades em C# continuam em PascalCase (ver `ags-developer`); nomes SQL devem ser escritos explicitamente em `snake_case`.

## Persistência com SQL explícito

- Toda leitura e escrita usa **SQL explícito via Npgsql**. Não usar ORM, micro-ORM ou LINQ de persistência para acessar o banco.
- O SQL deve ser parametrizado, ter o schema explícito e ficar próximo do repositório/query que o utiliza. Nunca concatenar valores recebidos da aplicação no texto SQL.
- Ao definir ou alterar um padrão de SQL, revisar retroativamente todas as migrations, repositories, serviços e ferramentas do projeto que contenham SQL; a convenção não vale apenas para arquivos novos.
- O SQL deve ser formatado para leitura visual: nunca concentrar uma instrução inteira em uma única linha.
- Em `INSERT`, `SELECT`, `UPDATE` e `DELETE`, cada coluna/campo deve ficar em sua própria linha, com indentação consistente.
- Em `SELECT`, cada expressão selecionada deve ocupar sua própria linha e ser qualificada pelo alias da tabela quando houver mais de uma tabela envolvida.
- Aliases devem ser claros e representar o conteúdo da tabela, como `refreshTokens`, `users`, `tokenTypes` e `outboxMessages`. Não usar aliases genéricos de uma letra, como `r`, `u`, `t` ou `x`.
- As cláusulas (`FROM`, `JOIN`, `WHERE`, `VALUES`, `SET`, `ORDER BY`, `GROUP BY`, `RETURNING` e semelhantes) devem começar em linhas próprias.
- `FROM` e cada `JOIN` devem ficar em uma linha própria, com a tabela e o alias na linha seguinte. A condição `ON` deve ficar em linha própria e suas condições adicionais devem começar com `AND` em nova linha.
- `WHERE` deve ficar em uma linha própria, com a primeira condição na linha seguinte. Toda condição adicional deve começar com `AND` em nova linha, nunca ficar colada na condição anterior.
- Listas de colunas e valores devem manter a mesma ordem visual sempre que houver correspondência entre elas.
- Quando uma instrução SQL for usada como string multilinha no C#, preservar o mesmo formato legível do SQL executado no banco; não compactar a string para economizar linhas.
- No código C# que executa SQL, separar visualmente as etapas de criação do comando, configuração dos parâmetros, execução, leitura do resultado e mapeamento com linhas em branco. Não colar essas etapas em uma única linha.
- Blocos condicionais da persistência devem sempre usar chaves e linhas próprias; `return` e `throw` nunca devem aparecer na mesma linha do `if`.
- Contratos específicos ficam em `Application`; implementações ficam em `Infrastructure`.
- A infraestrutura compartilhada fornece `IDbConnectionFactory`, `DbSession` e o controle de `DbTransaction`.
- Cada método retorna tipos fortes (`Domain`, DTO ou `record`), nunca `DataTable`, `DataSet`, `object` ou `byte[]` para transportar registros.
- O mapeamento de linhas deve ser explícito e testável, tratando `NULL` conscientemente.
- Todos os métodos de banco são assíncronos e recebem `CancellationToken`.
- A camada de persistência não traduz exceções para HTTP/WCF; essa tradução pertence à borda da aplicação.
- Não criar uma classe universal com estado mutável de parâmetros. Parâmetros pertencem ao comando atual e conexão/transação pertencem à sessão atual.
- Nenhum ORM ou micro-ORM faz parte da arquitetura. Consultas complexas continuam sendo SQL explícito com Npgsql.

Exemplo obrigatório de formatação:

```sql
INSERT INTO auth.refresh_tokens (
    external_id,
    created_at,
    updated_at,
    user_id,
    token_hash,
    expires_at,
    revoked_at
)
SELECT
    @external_id,
    @created_at,
    @updated_at,
    id,
    @token_hash,
    @expires_at,
    @revoked_at
FROM auth.users
WHERE external_id = @user_external_id;
```

Formato obrigatório para `SELECT` com `JOIN`:

```sql
SELECT
    refreshTokens.id,
    refreshTokens.external_id,
    refreshTokens.user_id,
    users.id AS user_id,
    users.external_id AS user_external_id
FROM
    auth.refresh_tokens AS refreshTokens
INNER JOIN
    auth.users AS users
    ON users.id = refreshTokens.user_id
WHERE
    refreshTokens.token_hash = @token_hash
    AND refreshTokens.revoked_at IS NULL
LIMIT 1;
```

```csharp
// Application/Queries/IUserLoginAttemptsReportQuery.cs
public interface IUserLoginAttemptsReportQuery
{
	Task<IReadOnlyList<UserLoginAttemptsReportItem>> ExecuteAsync(
		DateTime from,
		DateTime to,
		CancellationToken cancellationToken
	);
}

public sealed record UserLoginAttemptsReportItem(
	string Login,
	int FailedAttempts,
	DateTime? LastAttemptAt
);
```

## Evolução

Esta skill é o lugar para acumular, com o tempo, convenções mais específicas (padrão de nome de chaves estrangeiras e índices, uso de tipos específicos do Postgres, estratégia de seed de dados, etc.) à medida que forem sendo definidas.
