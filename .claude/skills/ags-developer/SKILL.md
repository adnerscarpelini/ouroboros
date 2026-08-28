---
name: ags-developer
description: Convenções de desenvolvimento C#/.NET do projeto Ouroboros — branch de trabalho, regras de commit/push, idioma, arquitetura limpa e padrões de nomenclatura/formatação de código. Use sempre que for escrever, revisar, refatorar ou sugerir código C# neste projeto.
---

# ags-developer

Skill base para atuar como desenvolvedor no projeto Ouroboros. Segue estas regras ao trabalhar com código C#/.NET neste repositório.

## Controle de versão

- A branch de trabalho padrão é `development`. Todo desenvolvimento novo acontece nela, nunca diretamente na `main`.
- Antes de começar a trabalhar, confirme que está na `development` (ou numa branch derivada dela). Se estiver na `main`, troque antes de editar código.
- **Nunca faça commit ou push automaticamente.** Edite/crie os arquivos normalmente, mas deixe o commit e o push sempre a cargo do usuário — apenas avise que as mudanças estão prontas para revisão.

## Idioma

- Nomes de classes, métodos, propriedades/atributos, variáveis, tabelas, colunas e demais identificadores de código: sempre em **inglês**.
- Comentários e documentação (XML docs, README, markdown, etc.): sempre em **português do Brasil**.
- Comentários só quando o "porquê" não é óbvio pelo código. Evitar comentário óbvio explicando o que o código já diz por si.

## Arquitetura e código limpo

- Priorizar Clean Architecture e Clean Code: separação clara de camadas (Domain, Application, Infrastructure, API/Presentation), regras de negócio isoladas do framework.
- Escrever só o necessário para o problema atual — evitar abstrações prematuras, generalizações especulativas, código morto ou duplicação desnecessária (KISS/YAGNI).
- Preferir nomes descritivos que dispensem comentário explicativo.

## Nomenclatura (casing)

- **camelCase**: variáveis locais e campos privados (ex.: `_orderStatus`, `totalAmount`).
- **PascalCase**: classes, métodos, propriedades públicas e demais membros públicos — padrão idiomático do C#/.NET (ex.: `OrderStatus`, `CalculateTotal()`). Não usar camelCase em propriedades públicas.

## Formatação de assinaturas de métodos

- Método/construtor com **0 ou 1 parâmetro**: assinatura em uma única linha.
- Método/construtor com **2 ou mais parâmetros**: quebrar um parâmetro por linha.

```csharp
// 1 parâmetro: uma linha
public void SetStatus(OrderStatus status) { ... }

// 2+ parâmetros: um por linha
public void Insert(
	string userId,
	string fullName,
	string userLogin,
	string passwordHash,
	int userStatusId,
	int userProfileId,
	int? personId
)
{
	...
}
```

- Nas chamadas de métodos com 2 ou mais parâmetros, sempre usar **named arguments**, um por linha, para facilitar a leitura:

```csharp
userService.Insert(
	userId: userId,
	fullName: fullName,
	userLogin: userLogin,
	passwordHash: passwordHash,
	userStatusId: userStatusId,
	userProfileId: userProfileId,
	personId: personId
);
```
