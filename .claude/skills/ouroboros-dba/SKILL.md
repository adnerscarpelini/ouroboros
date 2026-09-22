---
name: ouroboros-dba
description: Convencoes de banco de dados do projeto Ouroboros .NET — PostgreSQL, isolamento de banco por servico, migrations com DbUp, padrao de entidade persistida (id interno + external id + auditoria) e SQL explicito via Dapper. Use sempre que for criar ou alterar uma entidade que precisa ser persistida, escrever uma migration, decidir schema/nome de tabela/coluna, implementar um repositorio (gateway) no projeto de Infrastructure, ou tomar qualquer decisao relacionada a acesso a dados neste projeto. Ative mesmo que o pedido nao mencione "banco de dados" explicitamente — pedidos como "salva isso no banco", "cria a tabela de X", "adiciona uma coluna", "cria o repositorio de Y" ou "escreve a migration" ja sao gatilho.
---

# Ouroboros DBA

Complementa a [ouroboros-dev](../ouroboros-dev/SKILL.md) (arquitetura geral e convencoes de codigo): aqui ficam as decisoes especificas de banco de dados — schema, migrations, formato do SQL e o padrao de entidade persistida. As regras gerais de nomenclatura, idioma e controle de versao da `ouroboros-dev` valem integralmente aqui tambem; nao sao repetidas.

> Migrado da versao anterior em Java/Spring Boot: la a ferramenta de migration era Flyway e a execucao de SQL era via `JdbcTemplate`. Aqui os equivalentes sao DbUp e Dapper — ver as secoes correspondentes abaixo pras diferencas que isso implica.

## Banco de dados

- SGBD: **PostgreSQL**.
- Uma unica instancia Postgres compartilhada entre todos os servicos — nao um container por servico. Sobe via `docker-compose.yml` na raiz do monorepo (projeto/stack `ouroboros`), servico `postgres`; ver `.env.example` pras variaveis necessarias.

## Banco e schema por servico

- Cada servico (`<servico>-service/`) tem seu proprio banco logico na instancia compartilhada, nomeado `ouroboros_<servico>` (ex.: `auth-service` → banco `ouroboros_auth`), com uma role propria dona desse banco (ex.: `auth_service`). Isso e o que garante isolamento real entre servicos — nenhuma credencial de um servico alcanca o banco de outro — reforcando a regra da `ouroboros-dev` de que servicos nunca compartilham dependencia entre si.
- Dentro do banco do servico, as tabelas de negocio ficam no schema `<servico>` (ex.: schema `auth`, tabela `auth.users`). Ainda nao existe um schema tecnico compartilhado entre servicos (equivalente a um `common` de tabelas cross-cutting); se isso surgir no futuro, esta secao e o lugar pra documentar a decisao.
- Banco e role de cada servico sao criados por um script em `docker/postgres/init/` (ex.: `01-create-auth-db.sh` pro `auth-service`), executado automaticamente pelo Postgres na primeira subida do container — nunca criados manualmente. O script tambem **revoga `CONNECT`/`TEMPORARY` de `PUBLIC`** no banco criado: o Postgres concede conexao a `PUBLIC` por padrao, e sem isso a role de um servico alcancaria o banco de outro — o isolamento precisa ser real, nao so convencao. Ao adicionar um novo servico com persistencia, crie o proximo `NN-create-<servico>-db.sh` seguindo o mesmo padrao. Scripts `.sh` rodam dentro de um container Linux e precisam de finais de linha LF — o `.gitattributes` da raiz garante isso. (Esses scripts sao bash puro, independentes da linguagem da aplicacao — nao mudam com a migracao pra .NET.)

## Migrations

- Estrategia SQL-first: migrations sao arquivos `.sql` escritos a mao, nunca gerados a partir de um modelo de ORM.
- Ferramenta: **DbUp** (pacote NuGet `dbup-postgresql`), nao um runner proprio — o DbUp ja rastreia o historico aplicado numa tabela propria (`SchemaVersions` por padrao) e, por padrao, cada script so roda uma vez; se um arquivo ja aplicado for alterado, o comportamento de checksum precisa ser configurado explicitamente no `MigrationRunner` (ver abaixo) — nao confie no padrao sem revisar.
- Local dos arquivos: `<servico>-service/{Servico}.Infrastructure/Migrations/*.sql`, marcados como `<EmbeddedResource>` no `.csproj` do projeto de Infrastructure — e o padrao que o DbUp le via `WithScriptsEmbeddedInAssembly`, e mantem o SQL fisicamente perto da camada de persistencia que o usa.
- Nome do arquivo: DbUp nao exige um formato especifico (ele so ordena os scripts lexicograficamente pelo nome), mas o projeto adota o mesmo formato usado antes, prefixado com timestamp, pra manter a ordem cronologica explicita como identificador de versao:
  ```
  V<AAAAMMDDHHMMSS>__Descricao.sql
  ```
  Exemplos:
  ```
  V20260921154500__CreateUsersTable.sql
  V20260921160112__AddEmailConfirmedAtToUsers.sql
  ```
  O prefixo `V` e o duplo underscore (`__`) antes da descricao sao convencao do projeto (nao exigencia do DbUp), mantidos por continuidade e porque garantem ordenacao lexicografica correta. A descricao usa PascalCase, sem espacos.
- Depois de aplicada em qualquer ambiente compartilhado, uma migration e imutavel — correcoes vao em uma nova migration, nunca reescrevendo o historico.
- DDL destrutivo exige revisao explicita; para mudancas incompativeis, prefira o padrao expandir/migrar/contrair em vez de alterar uma coluna existente de uma vez so.
- Seeds e dados de referencia sao migrations idempotentes versionadas, nunca codigo de inicializacao da aplicacao.
- Como aplicar: DbUp nao tem autoconfiguracao como o par Flyway + Spring Boot — a execucao e explicita. Um `MigrationRunner` roda no inicio de `Program.cs`, antes de `app.Run()`, chamando `DeployChanges.To.PostgresqlDatabase(connectionString).WithScriptsEmbeddedInAssembly(typeof(Program).Assembly).LogToConsole().Build().PerformUpgrade()`. Por padrao deixe isso rodar sempre na subida da `-api` (e o caminho mais simples). Se algum dia isso for um risco (ambiente compartilhado, banco de producao), da pra desligar via uma flag de configuracao (`Migrations:RunOnStartup=false`) e aplicar via um passo deliberado (um alvo `dotnet run --project <servico>-service/{Servico}.Infrastructure -- migrate` ou similar) — mas comece pelo caminho simples.
- Palavras-chave SQL em maiusculo (`CREATE TABLE`, `ALTER TABLE`, `NOT NULL`, `PRIMARY KEY`, `FOREIGN KEY`, `REFERENCES`, etc.); nomes de schema/tabela/coluna em `snake_case`; nomes de tipo (`bigint`, `uuid`, `text`, `timestamptz`, `boolean`) em minusculo.

## Entidade base

Toda entidade persistida estende uma classe `Entity` local ao projeto `<Servico>.Domain` (cada servico declara a sua — nao criamos um projeto compartilhado entre servicos so pra isso, porque isso violaria a regra da `ouroboros-dev` de que cada `<servico>-service/` e um conjunto de projetos isolado; a classe e pequena o suficiente pra duplicar sem dor). Ela carrega quatro colunas presentes em toda tabela do projeto, sempre nessa ordem fisica nas migrations:

1. `id` (`long` / `bigint`, identity) — chave primaria interna, usada em joins e FKs. Nunca exposta pelo controller.
2. `external_id` (`Guid` / `uuid`, unico, gerado com `Guid.NewGuid()` na criacao) — identificador publico, usado em rotas/records da API. Nao revela volume nem ordem de criacao como um `id` sequencial exposto revelaria.
3. `created_at` (`timestamptz`, UTC) — carimbado automaticamente na criacao, dentro do construtor de `Entity`. Mapeado como `DateTimeOffset` em C#, nunca `DateTime` — `DateTimeOffset` carrega o offset explicitamente e evita ambiguidade de fuso horario que `DateTime` (mesmo com `Kind=Utc`) pode introduzir ao serializar/desserializar.
4. `updated_at` (`timestamptz`, UTC, nullable) — `null` ate a primeira alteracao; atualizado explicitamente pelo repositorio no mesmo comando SQL que persiste a alteracao. Mapeado como `DateTimeOffset?`.

```csharp
namespace Ouroboros.{Servico}.Domain.Entities;

public abstract class Entity
{
    public long Id { get; private set; }

    public Guid ExternalId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    protected Entity()
    {
        ExternalId = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    protected void RestorePersistence(long id, Guid externalId, DateTimeOffset createdAt, DateTimeOffset? updatedAt)
    {
        Id = id;
        ExternalId = externalId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public void MarkAsUpdated()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

As propriedades sao `{ get; private set; }` de proposito: precisam ser preenchidas tanto pelo caminho de criacao (construtor) quanto pelo caminho de reidratacao a partir do banco (`RestorePersistence`), mas continuam so alteraveis de dentro da propria classe — em C#, `private set` ja e exatamente isso, sem precisar de nenhum artificio adicional.

## Entidades persistidas (reidratacao)

Toda entidade que estende `Entity` precisa, alem do construtor/fabrica "de verdade" com as regras de negocio, de uma fabrica estatica `Rehydrate(...)` usada so pelo mapeamento Dapper pra reconstruir a entidade a partir de uma linha do banco:

```csharp
namespace Ouroboros.{Servico}.Domain.Entities;

using Ouroboros.{Servico}.Domain.Exceptions;

public sealed class User : Entity
{
    public string Name { get; private set; } = null!;

    private User()
    {
    }

    public static User Create(string name)
    {
        var user = new User
        {
            Name = ValidateName(name),
        };

        return user;
    }

    public static User Rehydrate(long id, Guid externalId, DateTimeOffset createdAt, DateTimeOffset? updatedAt, string name)
    {
        var user = new User
        {
            Name = name,
        };

        user.RestorePersistence(id, externalId, createdAt, updatedAt);

        return user;
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Name is required");
        }

        return name.Trim();
    }
}
```

Propriedades de referencia nao-nulas (`string`, nunca opcionalmente nulas sem necessidade) devem ser preenchidas tanto em `Create` quanto em `Rehydrate`; o `= null!` no campo so existe pra satisfazer o compilador com `Nullable` habilitado entre a declaracao e a atribuicao no construtor/factory, nunca representa um estado real de "ainda nao preenchido" fora do processo de construcao.

> O `User` do `auth-service` ainda nao existe nesta versao .NET do projeto — este e o primeiro servico a ser (re)construido do zero seguindo este padrao, entao nao ha migracao retroativa a fazer aqui como havia na versao anterior.

## Persistencia com SQL explicito

- Toda leitura e escrita usa SQL explicito via **Dapper** (pacote NuGet `Dapper`) sobre `NpgsqlConnection` (pacote `Npgsql`) — nunca Entity Framework Core, nem outro ORM com change tracking ou geracao de SQL a partir de LINQ.
- **Nuance importante**: Dapper e um *micro-ORM* no sentido tecnico (mapeia resultado de SQL pra objetos), mas e o equivalente direto do `JdbcTemplate` da versao Java — ele nao gera SQL, nao rastreia entidades, nao tem `DbContext`/unit-of-work implicito. A regra "sem ORM" da `ouroboros-dev` se refere a esse tipo de ORM com comportamento automatico (EF Core com change tracking, migrations geradas de modelo); Dapper e "SQL explicito com mapeamento de resultado", que e exatamente o que este projeto quer.
- Metodos de repositorio sao **assincronos** por padrao (`Task`/`Task<T>`, usando `QueryAsync`/`ExecuteAsync`/`QuerySingleAsync` do Dapper) — ver a nota em [ouroboros-dev](../ouroboros-dev/SKILL.md#2-servicoapplication--casos-de-uso-e-gateways) sobre por que isso difere da versao Java (que era sincrona por padrao).
- SQL parametrizado, com schema explicito, definido proximo do repositorio que o usa. Nunca concatenar valor recebido da aplicacao no texto SQL.
- Ao mudar um padrao de formatacao de SQL, revise retroativamente as migrations e repositorios existentes — a convencao vale pro projeto todo, nao so pra arquivos novos.
- Formatacao: nunca uma instrucao inteira numa linha so.
  - Em `INSERT`/`SELECT`/`UPDATE`/`DELETE`, cada coluna/expressao numa linha propria, indentacao consistente.
  - Em `SELECT` com mais de uma tabela, qualifique cada expressao pelo alias da tabela.
  - Aliases descritivos (`refreshTokens`, `users`), nunca de uma letra (`r`, `u`).
  - `FROM`, `JOIN`, `WHERE`, `VALUES`, `SET`, `ORDER BY`, `GROUP BY`, `RETURNING` comecam em linha propria; `JOIN` leva a tabela/alias na linha seguinte e a condicao `ON` em linha propria, com `AND` adicional em nova linha.
  - `WHERE` numa linha propria, primeira condicao na linha seguinte, condicoes extras sempre comecando com `AND` em nova linha.
  - Ao usar a instrucao como raw string literal C# (`"""..."""`), preserve a mesma formatacao legivel — nao compacte pra economizar linha.
- No codigo C# que executa SQL, separe visualmente com linha em branco: montar o SQL/parametros, executar, mapear o resultado, retornar.
- Blocos condicionais na camada de persistencia sempre usam chaves e linhas proprias (ver `ouroboros-dev`); `return`/`throw` nunca na mesma linha do `if`.
- Cada metodo de repositorio retorna um tipo forte — a entidade de dominio (via `Rehydrate`) ou um record de projecao — nunca `dynamic`, `DataTable` ou `IDataReader` cru.
- Mapeamento de linha explicito e testavel, tratando `NULL` conscientemente. Como as entidades tem construtor privado, o Dapper nao consegue instancia-las diretamente por convencao — o mapeamento e feito manualmente, consultando as colunas da linha (`dynamic`/`Query<T>` com um tipo de projecao interno, ou um `Func<...>` de mapeamento passado pro Dapper) e chamando `Entidade.Rehydrate(...)` explicitamente.
- A camada de persistencia nao traduz excecoes para HTTP — essa traducao e do controller (ver `ouroboros-dev`, tratamento de `DomainException`).

Exemplo de formatacao obrigatoria:

```sql
INSERT INTO auth.refresh_tokens (
    id,
    external_id,
    created_at,
    updated_at,
    user_id,
    token_hash,
    expires_at,
    revoked_at
)
SELECT
    nextval('auth.refresh_tokens_id_seq'),
    @ExternalId,
    @CreatedAt,
    @UpdatedAt,
    users.id,
    @TokenHash,
    @ExpiresAt,
    @RevokedAt
FROM auth.users AS users
WHERE users.external_id = @UserExternalId;
```

Formato obrigatorio para `SELECT` com `JOIN`:

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
    refreshTokens.token_hash = @TokenHash
    AND refreshTokens.revoked_at IS NULL
LIMIT 1;
```

Parametros Dapper usam `@NomeDoParametro` (PascalCase, casando com a propriedade do objeto anonimo/record passado como `param`), nunca `:nomeDoParametro` (sintaxe de outras bibliotecas) nem concatenacao de string.

## Segredos e connection string

- A connection string com a senha real nunca vai pro `appsettings.json` versionado.
- Localmente e em container, ela chega por variavel de ambiente `ConnectionStrings__Default` (duplo underscore — e a convencao do `IConfiguration` do .NET pra mapear pra uma chave aninhada `ConnectionStrings:Default`), sem precisar de arquivo adicional. Se for conveniente carregar essa variavel de um arquivo local, ele nunca e versionado (o `.env` ja esta no `.gitignore` da raiz).

## Specs

Trabalho de persistencia normalmente vem de uma spec aprovada pela [ouroboros-ba](../ouroboros-ba/SKILL.md), salva em `specs/{ano}/{mes}/{codigo}-{Titulo}.md`. Ao concluir uma tarefa sua listada la (`**DBA** — ...`), marque a caixa correspondente (`- [ ]` → `- [x]`) direto no arquivo da spec.

## Evolucao

Esta skill acumula, com o tempo, convencoes mais especificas de banco (nome de FKs/indices, tipos especificos do Postgres, estrategia de seed) a medida que forem sendo definidas.
