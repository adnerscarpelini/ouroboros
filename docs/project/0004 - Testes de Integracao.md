# Testes de Integração

Projeto `test/auth-service/Auth.Integration.Tests`. Testa a API e o SQL de verdade: SQL Server real, middleware JWT, rate limiting e transações.

Os testes de `Auth.Domain.Tests` e `Auth.Application.Tests` continuam usando repositório falso. Esta suíte cobre o que eles não alcançam.

## Pré-requisitos

- .NET SDK 10.
- Docker rodando. É o único requisito: não precisa de banco instalado, de `docker compose up` nem de `.env`.
- Na primeira execução o Docker baixa a imagem `mcr.microsoft.com/mssql/server:2022-latest`, a mesma do `docker-compose.yml`.

## Como rodar

```
dotnet test test/auth-service/Auth.Integration.Tests
```

A solution inteira, com os três projetos de teste:

```
dotnet test Ouroboros.slnx
```

Um teste ou uma classe:

```
dotnet test test/auth-service/Auth.Integration.Tests --filter "FullyQualifiedName~SessionApiTests"
```

## Como funciona

- **Um container por execução.** `AuthApiFixture` sobe um SQL Server descartável (Testcontainers) e aplica as migrations pelo `MigrationRunner`, antes de subir a API (a API não roda migrations no startup). O container some no fim.
- **API em memória.** `AuthApiFactory` (`WebApplicationFactory<Program>`) sobe a API real, sem porta de rede. Connection string, chave JWT e limites de rate limit são injetados com `UseSetting`, para valerem já no startup, onde o `Program` roda as migrations.
- **Segredos só de teste.** A chave JWT (`AuthApiFactory.SigningKey`) e a senha dos usuários de teste são fixas e não valem em lugar nenhum. Nada aponta para banco compartilhado.
- **Banco zerado entre testes.** Cada classe chama `ResetDatabaseAsync` em `InitializeAsync`. As classes compartilham o container pela collection `AuthApi`, então não rodam em paralelo.
- **Limite por IP.** O `TestServer` não tem conexão TCP. O IP de origem vem do header `X-Test-Remote-Ip`, e `TestApi` usa um IP novo por chamada para o rate limit não atrapalhar os outros testes. Os limites de teste são baixos (`AuthApiFixture.LowPermitLimit`, 3).

## Como escrever um teste novo

1. Crie a classe em `Api/` (fluxo pela API), `Persistence/` (SQL dos repositórios) ou na pasta da spec, como as de `Auth/`.
2. Marque com `[Collection(AuthApiCollection.Name)]`, receba o `AuthApiFixture` no construtor e implemente `IAsyncLifetime` zerando o banco:

   ```csharp
   [Collection(AuthApiCollection.Name)]
   public sealed class MeuFluxoApiTests : IAsyncLifetime
   {
       private readonly AuthApiFixture _fixture;
       private readonly TestApi _api;

       public MeuFluxoApiTests(AuthApiFixture fixture)
       {
           _fixture = fixture;
           _api = new TestApi(fixture);
       }

       public Task InitializeAsync() => _fixture.ResetDatabaseAsync();

       public Task DisposeAsync()
       {
           _api.Dispose();
           return Task.CompletedTask;
       }
   }
   ```
3. Use o `TestApi` para preparar e conferir:

   | Preciso de | Use |
   |---|---|
   | Chamar a API | `PostAsync`, `GetAsync`, `SendAsync` (aceitam `bearer` e IP) |
   | Usuário pronto | `CreateUserAsync(login, email, confirmed)` |
   | Admin | `SetRoleAsync(id, UserRole.Admin)` e depois `LoginAsync` |
   | Token de e-mail ou de reset em claro | `AddTokenAsync(id, TokenType...)` (na API ele só sai por e-mail) |
   | Token expirado | `ExpireTokenAsync`, `ExpireRefreshTokenAsync` |
   | Sessão logada | `LoginAsync(login)`, que devolve `AccessToken` e `RefreshToken` |
   | Conferir o banco | `QueryAsync`, `CountUsersAsync`, `CountTokensAsync`, `CountActiveRefreshTokensAsync` |
4. Nomeie no padrão `Should{Acao}When{Condicao}` e teste um comportamento por método.
5. Cada spec nova acrescenta aqui os próprios testes. Teste que descreve o comportamento antigo muda junto com a spec.

## Testar rollback com falha controlada

`FaultInjection.cs` troca um repositório por um decorator que lança `InjectedFaultException` na N-ésima chamada de um método. O repositório real continua sendo criado pelo DI.

```csharp
using var factory = _fixture.CreateFactoryFailingOn<ITokenRepository>(
    nameof(ITokenRepository.AddAsync));
using var api = new TestApi(_fixture, factory.CreateClient());

var response = await api.PostAsync("/api/users", body);

Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
Assert.Equal(0, await _api.CountUsersAsync());
```

- `FaultTiming.Before` (padrão): falha antes de o comando chegar ao banco.
- `FaultTiming.After`: o comando roda e só depois a exceção sai. É o que prova que o rollback desfaz uma escrita já feita.
- `onCall`: em qual chamada falhar (padrão, a primeira). A contagem vale por request.
- Descarte a factory com `using`.

## Armadilhas

- **Concorrência.** Para provar que um UPDATE condicional resolve uma disputa, leia todas as entidades antes e dispare só a escrita em paralelo. Se a leitura entrar na disputa, a entidade já recusa a operação antes do SQL.
- **`[Authorize]` antes do rate limit.** Em endpoint protegido, a requisição sem token recebe `401` e nunca chega ao limite. Para testar o `429`, mande um token válido.
- **Deixou de ser baseline.** Quando uma spec mudar um comportamento coberto aqui, atualize o teste e registre em `evidence/` da spec.
