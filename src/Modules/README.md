# Modules

Cada módulo aqui dentro representa um contexto de negócio (bounded context) isolado — ex.: `Catalog`, `Orders`, `Payments` — preparado para, futuramente, ser extraído como um serviço independente.

## Convenção de um módulo

```
src/Modules/<NomeDoModulo>/
├── Ouroboros.Modules.<NomeDoModulo>.Domain/
├── Ouroboros.Modules.<NomeDoModulo>.Application/
└── Ouroboros.Modules.<NomeDoModulo>.Infrastructure/
```

Cada camada segue as mesmas regras já definidas para o projeto (ver [docs/0000 - Arquitetura.md](../../docs/0000%20-%20Arquitetura.md) e a skill `ags-developer`). O projeto `Infrastructure` só é criado quando o módulo realmente tiver algo pra colocar lá (ex.: persistência) — não é criado vazio por antecipação.

O módulo `Auth` é o primeiro exemplo dessa convenção em prática, com as três camadas: `Domain` (entidade `User`), `Application` (contratos `IUserService`/`IPasswordHasher`, sem dependência de infraestrutura) e `Infrastructure` (`UserService` e `Argon2PasswordHasher` — implementações reais, com `AuthDbContext` + `AuthModule.AddAuthModule` — ver [ags-dba](../../.claude/skills/ags-dba/SKILL.md)).

## Regra de isolamento entre módulos

Um módulo **nunca** referencia o `Domain` ou `Application` de outro módulo diretamente. Se um módulo precisar de algo de outro, isso passa por um contrato explícito (ex.: interface, evento) — nunca por uma `ProjectReference` direta entre módulos.

Todos os módulos podem depender de `src/Common/` (tipos e abstrações compartilhados entre módulos), mas nunca uns dos outros.

Essa regra é o que mantém a possibilidade de, no futuro, extrair um módulo inteiro para um serviço/repositório próprio sem precisar desembaraçar acoplamento escondido.
