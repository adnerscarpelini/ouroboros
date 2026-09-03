# Services

Cada serviço aqui dentro é um microsserviço isolado — um contexto de negócio (bounded context) com processo, banco e deploy próprios — ex.: `Auth`, `Cadastros`. Ver [docs/0000 - Arquitetura.md](../../docs/0000%20-%20Arquitetura.md) para a decisão completa.

## Convenção de um serviço

```
src/Services/<NomeDoServico>/
├── Ouroboros.Services.<NomeDoServico>.Api/
├── Ouroboros.Services.<NomeDoServico>.Domain/
├── Ouroboros.Services.<NomeDoServico>.Application/
└── Ouroboros.Services.<NomeDoServico>.Infrastructure/
```

Cada camada segue as mesmas regras já definidas para o projeto (ver [docs/0000 - Arquitetura.md](../../docs/0000%20-%20Arquitetura.md) e a skill `ags-developer`). O projeto `Infrastructure` só é criado quando o serviço realmente tiver algo pra colocar lá (ex.: persistência) — não é criado vazio por antecipação.

O `Auth` é o primeiro exemplo dessa convenção em prática, com `Domain` (entidades `User`, `Token`, `RefreshToken` e `TokenType` — esta última já nasce com os tipos `UserCreationValidation` e `PasswordReset` via seed de migration), `Application` (os casos de uso `UserRegistrationService`, `AuthenticationService` e `PasswordResetService`, mais os contratos de que eles dependem — `IUserRepository`, `IUnitOfWork`, `IPasswordHasher` — sem nenhuma dependência de infraestrutura) e `Infrastructure` (as implementações desses contratos: repositórios EF Core, `UnitOfWork`, `AuthDbContext` e `AuthModule.AddAuthModule` — ver [ags-dba](../../.claude/skills/ags-dba/SKILL.md) e [docs/0005](../../docs/0005%20-%20Repositórios%20e%20Unidade%20de%20Trabalho.md)).

Cada camada agrupa suas classes por tipo em subpastas (`Interfaces/`, `Models/`, `Services/`, `Persistence/`, `Persistence/Repositories/`, `Options/`) em vez de deixá-las soltas na raiz — ver [0000 - Arquitetura.md](../../docs/0000%20-%20Arquitetura.md#organização-de-pastas-dentro-de-um-projeto).

## Regra de isolamento entre serviços

Um serviço **nunca** referencia o `Domain` ou `Application` de outro serviço diretamente — nem por `ProjectReference`, nem lendo o banco de dados do outro. Se um serviço precisar de algo de outro, isso passa por um contrato explícito (HTTP, evento), nunca por acoplamento direto de código ou de dados.

Todos os serviços podem depender de `src/BuildingBlocks/` (código técnico compartilhado entre serviços), mas nunca uns dos outros. `BuildingBlocks` é só código: cada serviço persiste seus próprios dados no seu próprio banco, mesmo usando um tipo compartilhado (ex.: `ErrorLog`).

Diferente de um monolito modular, aqui essa regra não é só convenção de código — ela é reforçada por processo e banco separados: não tem como um serviço acidentalmente ler o `DbContext` do outro, porque eles nem compartilham o mesmo processo.
