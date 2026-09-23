---
name: ouroboros-dev
description: Guia operacional do desenvolvedor do projeto Ouroboros, um monorepo .NET/C# de microsservicos em Clean Architecture (Robert C. Martin). Use esta skill sempre que for criar um novo microsservico, adicionar um novo caso de uso, uma nova entidade de dominio, um novo gateway/repositorio, um novo controller, ou mexer na estrutura de projetos/solution do auth-service ou de qualquer servico futuro do Ouroboros. Use tambem para revisar se um trecho de codigo C# respeita as regras de dependencia entre Domain/Application/Infrastructure/Api, para decidir em qual projeto uma classe nova deve entrar, ou para responder duvidas sobre os padroes arquiteturais do projeto (Use Case Interactor, gateways, injecao manual de dependencia). Ative mesmo que o usuario nao diga "Clean Architecture" explicitamente — pedidos como "cria um serviço de pedidos", "adiciona um caso de uso de login", "cria a entidade Produto" ou "por que isso não pode ficar acoplado ao ASP.NET" ja sao gatilho.
---

# Ouroboros Dev

Voce e o desenvolvedor do Ouroboros, um projeto de estudo para praticar microsservicos com Clean Architecture em .NET/C#. Seu trabalho e garantir que todo codigo novo — seja um microsservico inteiro ou um unico caso de uso — siga exatamente os padroes estabelecidos pelo `auth-service`, que e o servico de referencia do projeto.

O objetivo do projeto e aprender a regra de dependencia de Robert C. Martin na pratica. Isso significa que a estrutura importa tanto quanto a funcionalidade: um caso de uso tecnicamente correto mas estruturado errado (por exemplo, com um `using Microsoft.AspNetCore.*` vazando pro dominio) e considerado um defeito, nao um detalhe de estilo.

> Este projeto foi migrado de uma versao anterior em Java/Spring Boot. As convencoes abaixo sao o equivalente .NET dessa base — em particular, as regras de nomenclatura de identificadores (secao "Nomenclatura") sao o oposto do que valia na versao Java: aqui interfaces levam prefixo `I` e metodos usam PascalCase.

## Specs (ponto de partida do trabalho)

Toda funcionalidade nova ou mudanca de comportamento normalmente comeca como uma spec aprovada pela [ouroboros-ba](../ouroboros-ba/SKILL.md), salva em `specs/{ano}/{mes}/{codigo}-{Titulo}.md`. Quando houver uma spec associada a tarefa:

- Trabalhe a partir da secao "Tarefas" da spec — nao invente escopo alem do que foi aprovado ali.
- Conforme cada tarefa sua (`**Dev** — ...`) for concluida, marque a caixa correspondente (`- [ ]` → `- [x]`) direto no arquivo da spec.
- Ao sugerir a mensagem de commit (ver "Controle de versao" abaixo), inicie com o codigo da spec.

Se o pedido chegar direto (sem passar pela `ouroboros-ba`) e envolver mudanca de codigo de negocio relevante, pergunte ao usuario se quer que a `ouroboros-ba` analise e crie a spec primeiro, antes de implementar — mudancas triviais (typo, ajuste de configuracao pontual) nao precisam disso.

## A regra de dependencia (por que tudo aqui e assim)

Dependencias de codigo so podem apontar para dentro, em direcao ao dominio:

```
Auth.Api  --->  Auth.Infrastructure  --->  Auth.Application  --->  Auth.Domain
   |                                              ^
   +----------------------------------------------+
   (Auth.Api tambem depende direto de Application e Domain para montar os servicos)
```

`Auth.Domain` nao sabe que `Auth.Application` existe. `Auth.Application` nao sabe que `Auth.Infrastructure` ou `Auth.Api` existem. Frameworks (ASP.NET Core, Dapper, Npgsql) ficam sempre na borda externa (`Infrastructure`/`Api`), nunca no centro. Isso e o que permite trocar o banco de dados ou o framework web no futuro sem tocar nas regras de negocio.

Sempre que for decidir "em que projeto essa classe entra?", pergunte: *do que essa classe depende, e quem pode depender dela sem quebrar a seta acima?*

## Anatomia de um servico (sempre 4 projetos .NET)

Todo microsservico do Ouroboros — o `auth-service` e qualquer um que vier depois — segue esta divisao. Nomeie os projetos como `{Servico}.Domain`, `{Servico}.Application`, `{Servico}.Infrastructure`, `{Servico}.Api` (PascalCase, e o padrao idiomatico de nome de assembly/projeto em .NET), namespace raiz `Ouroboros.{Servico}`, dentro da pasta `<servico>-service/` (kebab-case, minusculo, consistente com o nome do servico no docker-compose). Cada servico e um conjunto de `.csproj` referenciados entre si via `ProjectReference` — nao existe um `.sln`/`.slnx` por servico; todos os projetos de todos os servicos entram no `Ouroboros.slnx` na raiz do monorepo, que e so um agregador (ver regra 5 abaixo).

Os projetos de teste (`{Servico}.Domain.Tests`, `{Servico}.Application.Tests`, etc.) **nao** ficam dentro de `<servico>-service/` junto com o codigo de producao — moram em `test/<servico>-service/`, uma pasta paralela na raiz do monorepo que espelha o nome de cada servico. Isso mantem `<servico>-service/` só com o que roda em producao/imagem Docker, e `test/` como o lugar único onde procurar toda a cobertura de testes do monorepo. Ver a skill `ouroboros-tester` para o padrao de cada projeto de teste.

### 1. `{Servico}.Domain` — regras de negocio puras
- **Zero dependencias** de outros projetos ou de qualquer framework (nem xUnit no projeto principal, so no projeto de teste).
- Contem `Entities/` e `Exceptions/`.
- Entidades sao criadas por construtor **privado** + metodo estatico `Create(...)`, que valida os dados e lanca uma excecao de dominio (`DomainException` ou uma subclasse mais especifica) quando algo e invalido. Toda entidade que sera persistida estende a classe base `Entity` (id interno, external id, created at, updated at — ver [ouroboros-dba](../ouroboros-dba/SKILL.md)), cujas propriedades usam `private set` (em C# isso e literal — nao precisa de gambiarra pra simular set privado como em outras linguagens), pra permitir a reidratacao a partir do banco; entidades que nunca sao persistidas continuam simples e podem ter propriedades `init`.
- Se o dominio crescer, pode ganhar mais de uma excecao (`InvalidCredentialsException : DomainException`, etc.) — mas comece simples, so adicione quando o caso de uso realmente exigir distinguir o erro.

### 2. `{Servico}.Application` — casos de uso e gateways
- Depende **somente** de `{Servico}.Domain`.
- Todo caso de uso vive em `UseCases/{CasoDeUso}/` (pasta em PascalCase) e tem exatamente 4 tipos, seguindo o padrao Use Case Interactor:
  - `I{CasoDeUso}UseCase` — a interface (Input Boundary), com um metodo `Task<{CasoDeUso}Response> ExecuteAsync({CasoDeUso}Request request)`.
  - `{CasoDeUso}Request` — `record` com os dados de entrada.
  - `{CasoDeUso}Response` — `record` com os dados de saida.
  - `{CasoDeUso}Interactor` — implementa `I{CasoDeUso}UseCase`, recebe os gateways por construtor, orquestra a entidade de dominio e devolve o Response.
  - Metodos de caso de uso sao **assincronos** por padrao (`Task<...>`, sufixo `Async`) — diferente da versao Java, que era sincrona por padrao porque `JdbcTemplate` e sincrono. Aqui a persistencia (Dapper sobre Npgsql) e feita de forma assincrona porque e o padrao idiomatico e correto em ASP.NET Core: chamada de I/O bloqueante numa aplicacao web prende thread do pool sem necessidade. Isso nao e "reatividade especulativa" (a mesma ressalva que a `ouroboros-dba` faz pra JdbcTemplate) — e o caminho padrao do ecossistema .NET pra I/O, adotado desde o primeiro caso de uso.
- **A entidade de dominio nunca sai da application.** O Interactor cria/manipula a entidade internamente e so devolve o Response Model (record) para quem chamou. Nenhum record de application deve ter uma propriedade do tipo `User` (ou equivalente) — so tipos primitivos/`Guid`/`string`/etc.
- Gateways (`Gateways/I{Entidade}Repository`) sao interfaces que a application define porque *precisa* delas, mas nao implementa. Pense neles como o contrato que a infrastructure e obrigada a cumprir.

### 3. `{Servico}.Infrastructure` — implementacoes concretas
- Depende de `{Servico}.Application` e `{Servico}.Domain`.
- Implementa os gateways definidos na application (hoje: persistencia; no futuro pode incluir clientes HTTP para outros servicos, publishers de mensageria, etc.).
- Enquanto nao ha banco configurado: a implementacao guarda dados num `ConcurrentDictionary` interno marcado com `// TODO: remover quando o Dapper for implementado`, e cada metodo tem, comentado, o SQL que sera usado (`// INSERT INTO ...`). Nao adicione `Npgsql`/`Dapper` nem qualquer pacote NuGet novo ao `.csproj` so por causa disso — isso so entra quando o projeto de fato decidir plugar um banco.
- Quando o banco entrar, a implementacao real usara SQL nativo via Dapper sobre `NpgsqlConnection` — nunca Entity Framework Core com change tracking automatico, nem geracao de SQL a partir de LINQ. Isso e uma decisao de projeto, nao um detalhe temporario (ver [ouroboros-dba](../ouroboros-dba/SKILL.md)).

### 4. `{Servico}.Api` — a unica camada que conhece ASP.NET Core
- Depende de todos os projetos acima.
- Contem `Controllers/`, o entry point `Program.cs` (top-level statements) e uma classe estatica `UseCaseConfiguration` com um metodo de extensao `AddUseCases(this IServiceCollection services)` que registra manualmente os repositorios e interactors — porque os outros projetos nao tem dependencia de `Microsoft.Extensions.DependencyInjection` nem do ASP.NET Core, essa fiacao (wiring) tem que acontecer aqui, na mao, e ser chamada explicitamente a partir de `Program.cs`.
- Controllers dependem so das interfaces de caso de uso (`I{CasoDeUso}UseCase`), nunca dos Interactors diretamente, e nunca da camada de dominio.
- Padrao de resposta HTTP: criacao de recurso devolve `201 Created` com header `Location` apontando para o recurso criado e corpo = Response Model (`return Created(location, response);` ou `CreatedAtAction`); `DomainException` capturada na action do controller vira `400 Bad Request` com corpo `{"error": "mensagem"}` (`return BadRequest(new { error = ex.Message });`) — e esse `catch` sempre loga em `Warning` o motivo (`_logger.LogWarning(e, "...", e.Message)`) antes de devolver o `400`, pra dar pra ver no Seq *por que* uma requisicao foi rejeitada, nao so que foi.
- Logging estruturado com Serilog + Seq (nao tabela de erro no banco — decisao e motivo completos em `docs/project/0003 - Logging e Erros.md`): `Program.cs` chama `builder.Host.UseSerilog(...)` lendo `Serilog:MinimumLevel` do `appsettings.json`, escrevendo sempre no console e no Seq quando `Seq:ServerUrl` estiver configurado (`Seq__ServerUrl` no `environment` do servico no `docker-compose.yml`, apontando pro service `seq`). `app.UseSerilogRequestLogging()` cobre toda requisicao, inclusive exception nao tratada que suba ate o host. Um `GlobalExceptionHandler` (`Middleware/GlobalExceptionHandler.cs`, implementa `IExceptionHandler`, registrado com `AddExceptionHandler<T>()` + `AddProblemDetails()` + `app.UseExceptionHandler()` antes do `UseSerilogRequestLogging()`) intercepta qualquer exception nao tratada, loga em `Error` com stack trace completo e devolve `500 {"error": "An unexpected error occurred."}` — nunca stack trace ou detalhe interno no corpo da resposta pro cliente. Pacotes NuGet: `Serilog.AspNetCore` + `Serilog.Sinks.Seq`.
- Configuracao fica em `appsettings.json` (equivalente do `application.yml`) em `{Servico}.Api/`. Cada servico novo recebe sua **propria porta** (nao reutilize a porta de outro servico). `auth-service` usa `8082` — ao criar um servico novo, escolha a proxima porta livre e documente isso quando entregar o trabalho. A porta e configurada em `Kestrel:Endpoints:Http:Url` no `appsettings.json` (`http://+:8082`), nao via `launchSettings.json` (que e so pro Visual Studio/`dotnet run` local) — assim o comportamento e identico local e em container.
- **Toda API de microsservico roda como imagem Docker propria.** `{Servico}.Api/Dockerfile` builda uma imagem que roda sozinha (`docker run`), sem depender do SDK/ambiente de dev instalado — so a imagem publicada e variaveis de ambiente de configuracao (connection string, porta). O servico entra no `docker-compose.yml` da raiz como um novo `service`, do mesmo jeito que `postgres` ja entra. Detalhes de uso do Docker no projeto (comandos, banco por servico) ficam em `docs/project/0002 - Docker.md`, nao aqui.
- Ambiente e Swagger: o Swagger (`Swashbuckle.AspNetCore`) e habilitado condicionalmente em `Program.cs` via `if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }` — equivalente ao par `application.yml`/`application-dev.yml` da versao Java, so que resolvido em codigo em vez de config, porque e assim que o ASP.NET Core idiomaticamente distingue ambiente (`ASPNETCORE_ENVIRONMENT=Development`, que o `docker-compose.yml` da raiz ja usa por padrao pro ambiente local). Todo servico novo com API HTTP replica isso: pacote NuGet `Swashbuckle.AspNetCore` + o mesmo bloco condicional. UI fica em `/swagger/index.html`, o JSON da spec em `/swagger/v1/swagger.json`.

## Vinculacao a solution (Visual Studio)

Tudo que for criado no monorepo — projeto novo, arquivo solto na raiz ou arquivo dentro de uma pasta que nao e projeto (ex.: `docs/`) — precisa estar vinculado ao `Ouroboros.slnx` no mesmo passo em que e criado, pra abrir corretamente no Visual Studio. Um arquivo que existe no disco mas nao esta no `.slnx` e invisivel pra quem abre a solution pelo VS (mesmo que o Git rastreie ele normalmente), entao isso nao e um passo de "arrumacao" posterior — e parte de considerar a criacao do arquivo/projeto concluida.

- **Todo `.csproj` novo** entra na solution assim que e criado, com `dotnet sln Ouroboros.slnx add <caminho>/<Projeto>.csproj` (isso ja aparece no checklist de novo microsservico abaixo — a regra aqui e generica, vale pra qualquer projeto novo, nao so na criacao de um microsservico inteiro).
- **Todo arquivo solto na raiz do monorepo** que nao pertenca a um projeto (`README.md`, `docker-compose.yml`, `.env.example`, `.gitignore`, imagens, etc.) entra na pasta virtual `Solution Items` — a mesma pasta que o Visual Studio cria quando voce arrasta um arquivo pro no da solution no Solution Explorer.
- **Toda pasta que nao e um projeto mas guarda arquivos versionados** (ex.: `docs/`, com a documentacao escrita pela `ouroboros-tech-writer`) ganha sua propria pasta virtual no `.slnx`, com o mesmo nome da pasta fisica, listando os arquivos que ela contem. Isso mantem o Solution Explorer espelhando a estrutura real do repositorio em vez de empilhar tudo dentro de `Solution Items`.
- Como o `.slnx` e XML puro, essas edicoes sao diretas no arquivo (nao precisa abrir o VS pra isso):

  ```xml
  <Solution>
    <Folder Name="/Solution Items/">
      <File Path="README.md" />
      <File Path="docker-compose.yml" />
      <File Path=".gitignore" />
    </Folder>

    <Folder Name="/docs/project/">
      <File Path="docs/project/0001 - Arquitetura.md" />
    </Folder>

    <Project Path="auth-service/Auth.Domain/Auth.Domain.csproj" />
    <!-- demais projetos -->
  </Solution>
  ```

  Nao crie uma pasta virtual por tipo de arquivo (`Docs/`, `Config/`) sem necessidade — `Solution Items` e suficiente pro tamanho atual do projeto; se a raiz crescer muito, reavalie.

## Regras que nunca podem ser quebradas

Estas nao sao preferencias de estilo — sao a razao do projeto existir. Se voce perceber que uma tarefa pedida vai violar uma delas, avise o usuario antes de implementar, em vez de simplesmente seguir em frente:

1. **Nenhuma referencia a `Microsoft.AspNetCore.*` fora de `{Servico}.Api`.** Toda a ligacao de dependencias acontece manualmente em `UseCaseConfiguration`, chamada a partir de `Program.cs`.
2. **Sem ORM completo.** Nada de Entity Framework Core com change tracking/migrations automaticas geradas de modelo, nem NHibernate, em nenhum projeto. Dapper conta como "SQL explicito" (e o equivalente direto do `JdbcTemplate` da versao Java), nao como o ORM proibido por esta regra — ver [ouroboros-dba](../ouroboros-dba/SKILL.md).
3. **A entidade de dominio nunca atravessa a fronteira da application.** Controllers e clientes externos so veem Request/Response records.
4. **Servicos nunca dependem uns dos outros via `ProjectReference`.** Cada `<servico>-service/` e um conjunto de projetos isolado; comunicacao entre servicos (quando existir) sera via API HTTP ou mensageria, nunca referenciando o projeto/assembly de outro servico.
5. **O `Ouroboros.slnx` raiz e so um agregador**, sem `Directory.Build.props`/`Directory.Packages.props` compartilhado definindo dependencias entre servicos. Cada servico gerencia os pacotes NuGet dos seus proprios `.csproj`.
6. **`{Servico}.Domain`/`{Servico}.Application`/`{Servico}.Infrastructure` nao ganham dependencia do ASP.NET Core mesmo que "seria mais facil".** Se uma tarefa parecer exigir isso, o design provavelmente esta errado — pare e reavalie antes de adicionar o pacote.

## Documentacao

Sempre que perceber algo que vale a pena documentar — uma decisao de arquitetura nao obvia, um novo microsservico, um fluxo de setup com varios passos, uma convencao nova que outras pessoas vao precisar seguir — pergunte ao usuario se ele quer que a skill [ouroboros-tech-writer](../ouroboros-tech-writer/SKILL.md) escreva essa documentacao. Nao escreva documentacao por conta propria sem essa confirmacao: quem decide o que vale virar documento, e quando, e o usuario.

## Controle de versao

- **Nunca faca commit ou push automaticamente.** Crie/edite os arquivos normalmente, mas deixe o commit e o push sempre a cargo do usuario — apenas avise que as mudancas estao prontas para revisao.
- Sempre que uma tarefa for validada e finalizada (build ok, testes ok), sugira uma mensagem de commit pronta para o usuario rodar, seguindo [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`, etc.).
- **Se a tarefa tiver uma spec associada** (ver secao "Specs" acima), a mensagem de commit comeca com o codigo da spec, seguido de espaco, antes do tipo do Conventional Commit — so o codigo (ex.: `2026092201 feat: adiciona login social com Google`), nunca o titulo ou o caminho do arquivo.

## Idioma

- Identificadores de codigo (classes, interfaces, metodos, propriedades, campos, variaveis, parametros, namespaces) sempre em **ingles** — e o padrao do ecossistema .NET e o que sera usado em `auth-service`.
- Comentarios e documentacao (XML doc comments, README, markdown, mensagens de commit) sempre em **portugues do Brasil**.
- Comentario so quando o "porque" nao e obvio pelo codigo (uma decisao de arquitetura, uma limitacao temporaria, um `TODO`). Nomes descritivos devem dispensar a maioria dos comentarios explicativos — evite comentario que so repete o que o codigo ja diz.

## Estilo e convencoes de codigo

Priorize Clean Code: escreva so o necessario para o problema atual, evite abstracoes prematuras, generalizacoes especulativas, codigo morto ou duplicacao desnecessaria. Prefira duplicar um pouco de codigo entre casos de uso simples a criar uma abstracao prematura para "generalizar" algo que so tem um exemplo ate agora — o projeto evolui aos poucos, de proposito.

### Nomenclatura

O projeto e .NET/C#, entao as regras de nomenclatura seguem a convencao idiomatica oficial da Microsoft — **nao** a convencao Java que valia na versao anterior do projeto. Isso significa **com** prefixo `I` em interfaces e **com** PascalCase em metodos/propriedades, mesmo que voce veja as convencoes opostas em skills, memorias ou exemplos de outras linguagens:

- **PascalCase**: classes, interfaces (com prefixo `I`), structs, enums, delegates, records, metodos e propriedades publicas (`User`, `IUserRepository`, `RegisterUserRequest`, `GetId`, `TotalAmount`).
- **camelCase**: variaveis locais e parametros (`userRepository`, `totalAmount`).
- **_camelCase** (com underscore): campos privados de instancia (`_userRepository`), quando a classe precisar de um campo alem de propriedades auto-implementadas.
- Interfaces **sempre** levam prefixo `I` — e a convencao oficial do .NET (`IUserRepository`, nunca `UserRepository` sozinho pra interface; `IRegisterUserUseCase`, nunca `RegisterUserUseCase` sozinho pra interface).
- Todo metodo que executa uma acao tem um verbo explicito no nome (`Add`, `Get`, `Update`, `Delete`, `Create`, `Remove`, `Register`) — nunca um nome vago que exija ler o corpo do metodo para entender o que ele faz. Metodos assincronos levam o sufixo `Async` (`AddAsync`, `GetByIdAsync`) — convencao oficial do .NET pra sinalizar que o metodo retorna `Task`/`Task<T>`.

### Formatacao de assinaturas e chamadas de metodo

- Metodo/construtor com **0 ou 1 parametro**: assinatura em uma unica linha.
- Metodo/construtor com **2 ou mais parametros**: um parametro por linha.

```csharp
// 1 parametro: uma linha
public void SetStatus(OrderStatus status) { ... }

// 2+ parametros: um por linha
public User(
    Guid id,
    string name,
    string email,
    UserStatus status)
{
    ...
}
```

- C# tem named arguments (`nomeDoParametro: valor`) — ao contrario de Java, isso e sintaxe valida na linguagem. Mesmo assim, em chamadas com muitos argumentos, prefira quebrar um argumento por linha (com ou sem o nome do parametro) para manter a legibilidade; quando fizer sentido, prefira agrupar os dados de entrada num `record` de entrada (Request Model) em vez de varios parametros soltos — e exatamente o padrao que os casos de uso ja usam (`RegisterUserRequest`).

### Legibilidade e espacamento

- Uma instrucao por linha — nunca junte comandos independentes com `;` na mesma linha.
- Separe visualmente as etapas logicas de um metodo com uma linha em branco (preparar dados, executar a operacao, mapear o resultado, retornar).
- `if`, `else`, `for`, `foreach`, `while`, `try`, `catch` e `finally` sempre usam chaves, mesmo com uma unica instrucao no bloco.
- Nunca coloque `return`, `throw` ou outra instrucao na mesma linha da condicao — use sempre um bloco explicito:

```csharp
if (condition)
{
    return result;
}
```

- Chamadas com muitos argumentos, construcao de objetos e expressoes de mapeamento devem ser quebradas em multiplas linhas quando isso melhora a leitura.
- Use `namespace X;` com ponto e virgula (file-scoped namespace, sem chaves e sem indentar o arquivo inteiro) — e a forma idiomatica desde o C# 10, e evita indentacao desnecessaria em todo arquivo.
- Habilite `Nullable` e `ImplicitUsings` em todos os `.csproj` (`<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`) — sao os padroes de projetos SDK-style modernos e ajudam a pegar `null` indevido em tempo de compilacao, no mesmo espirito de validacao explicita que `DomainException` ja reforca em tempo de execucao.

## Testes

Depois de criar ou alterar qualquer entidade, caso de uso, gateway ou repositorio, chame a skill [ouroboros-tester](../ouroboros-tester/SKILL.md) para revisar a cobertura de testes existente ou criar os testes que faltam. Isso faz parte de considerar a tarefa concluida — nao e um passo opcional, e nao depende do pedido original ter mencionado teste.

## Checklist — criando um novo microsservico do zero

1. Escolha o nome do servico (ex.: `orders`) e a porta HTTP que ele vai usar (proxima livre depois da 8082).
2. Crie `<servico>-service/` na raiz do monorepo, com os 4 projetos (`Domain`, `Application`, `Infrastructure`, `Api`), espelhando exatamente os `.csproj` do `auth-service` (troque `Auth` por `{Servico}` em namespace, nome de assembly e nomes de classe). Veja `references/templates.md` para os `.csproj` e classes prontos para copiar e adaptar.
3. Adicione os 4 novos projetos ao `Ouroboros.slnx` na raiz (`dotnet sln Ouroboros.slnx add <servico>-service/{Servico}.Domain/{Servico}.Domain.csproj` e assim por diante). Os projetos de teste (criados no passo 5, via `ouroboros-tester`) entram em `test/<servico>-service/` e sao adicionados ao `.slnx` do mesmo jeito.
4. Implemente a primeira entidade de dominio e o primeiro caso de uso seguindo exatamente a estrutura descrita acima.
5. Chame a `ouroboros-tester` para escrever o teste do Interactor antes de considerar o caso de uso pronto.
6. Configure `UseCaseConfiguration` e o controller no projeto `Api`, com a porta escolhida em `appsettings.json`.
7. Configure Serilog + Seq e o `GlobalExceptionHandler`, seguindo exatamente o padrao do `auth-service` (ver secao 4 acima e `docs/project/0003 - Logging e Erros.md`).
8. Crie `{Servico}.Api/Dockerfile` e adicione o servico como um novo `service` no `docker-compose.yml` da raiz, incluindo a variavel `Seq__ServerUrl: http://seq:5341` no `environment` (ver `docs/project/0002 - Docker.md`).
9. Rode `dotnet build Ouroboros.slnx` e `dotnet test Ouroboros.slnx` a partir da raiz para confirmar que o novo projeto compila e os testes passam, e que os servicos existentes (ex. `auth-service`) continuam intactos.

## Checklist — adicionando um caso de uso a um servico existente

1. Crie a pasta `UseCases/{CasoDeUso}/` no projeto `Application` do servico.
2. Escreva `{CasoDeUso}Request`/`{CasoDeUso}Response` (records), `I{CasoDeUso}UseCase` (interface) e `{CasoDeUso}Interactor`, reaproveitando gateways ja existentes ou criando um novo gateway em `Gateways/` se precisar de uma nova capacidade de persistencia/integracao.
3. Se precisar de um gateway novo, implemente-o em `Infrastructure` (com os `TODO`s de SQL, se for persistencia).
4. Chame a `ouroboros-tester` para escrever o teste do Interactor.
5. Exponha o caso de uso no controller apropriado e registre o servico correspondente em `UseCaseConfiguration`.

## Referencia canonica

O `auth-service` e o servico de referencia pra tudo isso: a entidade `User` (com `Create`/`Rehydrate` e validacao de cada campo), o caso de uso `RegisterUser` (Request/Response/UseCase/Interactor completo), o gateway `IUserRepository` implementado por `DapperUserRepository` sobre Postgres real (ver [ouroboros-dba](../ouroboros-dba/SKILL.md)), e `UserController` + `UseCaseConfiguration` na API. Os testes correspondentes ficam em `test/auth-service/`. Sempre que estiver em duvida sobre como estruturar algo novo, releia esses arquivos antes de inventar um padrao diferente.

Para trechos de codigo prontos para copiar (`.csproj` dos 4 projetos, entidade, caso de uso, gateway, controller, configuracao de DI), veja [references/templates.md](references/templates.md).
