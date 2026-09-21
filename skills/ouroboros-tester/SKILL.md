---
name: ouroboros-tester
description: Revisa a cobertura de testes unitarios do projeto Ouroboros e cria os testes que faltam, seguindo o padrao ja estabelecido no auth-service — repositorio fake escrito a mao, sem Moq/NSubstitute, cobrindo caminho feliz e validacao de dominio, com xUnit. Use esta skill sempre que criar ou alterar um caso de uso, uma entidade de dominio ou um gateway/repositorio no Ouroboros: a ouroboros-dev chama esta skill automaticamente depois de qualquer mudanca de codigo de producao, antes de considerar a tarefa concluida. Use tambem quando o usuario pedir diretamente pra escrever teste, revisar cobertura, ou perguntar "isso tem teste?"/"os testes ainda passam?".
---

# Ouroboros Tester

Voce e responsavel pela cobertura de testes unitarios do Ouroboros. Seu trabalho nao e "rodar os testes" — e garantir que todo comportamento de negocio novo ou alterado tenha um teste que falharia se esse comportamento quebrasse.

> Migrado da versao anterior em Java/Spring Boot (JUnit 5 + repositorio fake, sem Mockito). Aqui o equivalente e xUnit + repositorio fake, sem Moq/NSubstitute — o espirito do padrao (fake escrito a mao, nada de framework de mock nem de contexto de DI nos testes de caso de uso) e o mesmo, so a sintaxe muda.

## Quando agir

- Chamada pela [ouroboros-dev](../ouroboros-dev/SKILL.md) automaticamente, logo depois de qualquer criacao ou alteracao de codigo de producao (entidade, caso de uso, gateway, implementacao de repositorio, controller). Nao e uma sugestao opcional — faz parte de considerar a tarefa concluida.
- Tambem pode ser chamada direto pelo usuario ("escreve o teste disso", "revisa a cobertura", "os testes ainda passam depois dessa mudanca?").

## O que revisar a cada chamada

1. **Caso de uso novo** → precisa existir uma classe `{CasoDeUso}InteractorTests` cobrindo pelo menos:
   - o caminho feliz (dados validos: verifica o Response e que o repositorio foi chamado com o dado certo);
   - cada regra de dominio que deveria lancar `DomainException` (campo obrigatorio faltando, formato invalido, etc.), verificando tambem que **nada** foi persistido nesse caso.
2. **Caso de uso alterado** (novo campo no Request/Response, nova regra de validacao, novo gateway usado) → os testes existentes ainda fazem sentido? Se uma regra nova nao tem teste cobrindo ela, adicione um caso novo — nao deixe so os testes antigos passando como se nada tivesse mudado.
3. **Entidade de dominio com validacao propria** → se essa logica ja e inteiramente exercida pelos testes do caso de uso que a usa, nao duplique o teste. Se a entidade tiver uma regra que nenhum caso de uso testado hoje exercita, escreva um teste direto da entidade.
4. **Gateway novo** (a interface em si) → nao precisa de teste proprio, e so um contrato.
5. **Implementacao de infrastructure** (ex. `Dapper{Entidade}Repository`) → enquanto for a implementacao temporaria em `ConcurrentDictionary`, nao precisa de teste dedicado. Isso muda quando o SQL real (Dapper) entrar de fato — nesse momento, revisite esta secao.
6. **Controller** → o projeto nao usa teste de controller com `WebApplicationFactory`/servidor em memoria hoje (mesmo espirito de "sem framework pesado, sem host real nos testes"); a logica dele (chamar o caso de uso, traduzir `DomainException` em 400) ja fica coberta indiretamente pelo teste do Interactor. Se um controller ganhar logica propria alem disso, reavalie se ele passa a merecer teste dedicado.

## Padrao do teste (o que copiar)

Baseado no `RegisterUserInteractorTests` do `auth-service`:

- Fica no projeto `{Servico}.Application.Tests` (projeto de teste separado, referenciando `{Servico}.Application`), mesma estrutura de pasta do caso de uso (`UseCases/{CasoDeUso}/`), classe `{CasoDeUso}InteractorTests`.
- O repositorio fake e uma classe privada aninhada dentro do proprio arquivo de teste, implementando a interface do gateway e guardando os itens numa lista — nunca Moq/NSubstitute, nunca `IServiceProvider`/host de DI.
- Nome do metodo de teste descreve o cenario em PascalCase, no padrao `Should{Acao}When{Condicao}` (`ShouldRegisterUserWhenNameIsValid`, `ShouldThrowDomainExceptionWhenNameIsInvalid`).
- Metodos de teste sao `async Task`, marcados com `[Fact]`, porque o Interactor e assincrono (ver [ouroboros-dev](../ouroboros-dev/SKILL.md)).
- Cada teste verifica **um** comportamento — nao junte "caminho feliz" e "validacao" no mesmo metodo.

```csharp
namespace Ouroboros.{Servico}.Application.UseCases.{CasoDeUso};

using Ouroboros.{Servico}.Application.Gateways;
using Ouroboros.{Servico}.Domain.Entities;
using Ouroboros.{Servico}.Domain.Exceptions;
using Xunit;

public class {CasoDeUso}InteractorTests
{
    private sealed class Fake{Entidade}Repository : I{Entidade}Repository
    {
        public List<{Entidade}> Items { get; } = new();

        public Task AddAsync({Entidade} entity)
        {
            Items.Add(entity);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Should{Acao}When{CondicaoValida}()
    {
        var repository = new Fake{Entidade}Repository();
        var interactor = new {CasoDeUso}Interactor(repository);

        var response = await interactor.ExecuteAsync(new {CasoDeUso}Request(/* dados validos */));

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task ShouldThrowDomainExceptionWhen{CondicaoInvalida}()
    {
        var repository = new Fake{Entidade}Repository();
        var interactor = new {CasoDeUso}Interactor(repository);

        await Assert.ThrowsAsync<DomainException>(() => interactor.ExecuteAsync(new {CasoDeUso}Request(/* dado invalido */)));
        Assert.Empty(repository.Items);
    }
}
```

## Depois de revisar

- Se faltar teste, crie antes de considerar a tarefa concluida — nao entregue codigo de producao novo sem o teste correspondente.
- Se encontrar um teste desatualizado (testa um comportamento que nao existe mais), corrija-o em vez de deixar quebrado ou de apagar sem substituir.
- Rode `dotnet test` (ou `dotnet test <projeto>.Tests`) quando o .NET SDK estiver disponivel no ambiente, pra confirmar que os testes passam de verdade antes de reportar a tarefa como pronta. Se o SDK nao estiver disponivel, avise que a validacao nao pode ser confirmada localmente.

## Regras que nunca podem ser quebradas

- Sem Moq/NSubstitute e sem host de DI (`WebApplicationFactory`, `IServiceProvider` real) nos testes de caso de uso — o repositorio fake escrito a mao e o padrao do projeto.
- Sem teste vazio ou generico so pra "ter cobertura" — cada teste precisa verificar um comportamento real e falhar se esse comportamento quebrar.
- Teste desatualizado nao fica passando por acidente: se o comportamento mudou, o teste muda junto.
