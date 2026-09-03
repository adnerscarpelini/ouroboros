---
name: ags-devops
description: Convenções de infraestrutura, containers e deploy do projeto Ouroboros — Dockerfile por serviço, Docker Compose, profiles, portas, health checks, variáveis de ambiente e segredos. Use sempre que criar uma Api nova (toda Api nasce containerizada), mexer no docker-compose.yml ou em Dockerfile, configurar segredo/variável de ambiente, expor porta, ajustar rota ou rate limiting no Api Gateway, ou tomar qualquer decisão de infraestrutura ou deploy neste projeto.
---

# ags-devops

Skill base para infraestrutura, containers e deploy no projeto Ouroboros. Complementa a [ags-developer](../ags-developer/SKILL.md) (convenções gerais de código) e a [ags-dba](../ags-dba/SKILL.md) (banco, role e migrations — aqui fica só o container que hospeda o Postgres).

## Escopo

Tudo que envolve **rodar** o projeto, e não escrevê-lo: `Dockerfile`, `docker-compose.yml`, imagens, rede, portas, health checks, variáveis de ambiente, segredos, roteamento e rate limiting no Api Gateway.

Se a tarefa mexe em algum desses, siga esta skill — mesmo que o pedido tenha vindo por outro caminho (ex.: "criar o serviço de Cadastros" é `ags-developer` **e** `ags-devops`).

## Regra principal: toda Api nasce containerizada

Não existe serviço que roda só pela IDE. Ao criar um `Ouroboros.Services.<NomeDoServico>.Api`, estes itens entram na **mesma tarefa**, sem virar pendência:

1. **`Dockerfile`** dentro do projeto Api (ver seção abaixo).
2. **Serviço no `docker-compose.yml`**, no profile `apps`.
3. **`/health` e `/health/ready`** na Api, ambos `[AllowAnonymous]`.
4. **Banco e role** no script de `docker/postgres/init/` — o conteúdo SQL é decisão da [ags-dba](../ags-dba/SKILL.md), o arquivo e o container são daqui.
5. **Rota no Api Gateway** (`appsettings.json` do gateway) com a política de rate limiting adequada.
6. **Variáveis e segredos** do serviço no `docker-compose.yml` e, quando o desenvolvedor precisar defini-los, no `.env.example`.
7. **Porta interna de desenvolvimento** própria no `launchSettings.json` (ver tabela de portas).
8. **Documentação afetada** atualizada, seguindo a [ags-technical-writer](../ags-technical-writer/SKILL.md).

Um serviço sem container não está pronto: é o deploy independente que justifica microsserviços em vez de monolito modular ([docs/0000](../../../docs/0000%20-%20Arquitetura.md)).

## Dockerfile

Um por projeto executável (Api ou gateway), dentro da pasta do próprio projeto. Padrão em uso, a ser copiado:

- **Build em dois estágios**: compila no `mcr.microsoft.com/dotnet/sdk:10.0`, publica só o resultado no `mcr.microsoft.com/dotnet/aspnet:10.0`.
- **O contexto de build é a raiz do repositório**, não a pasta do projeto — os serviços referenciam projetos de `src/BuildingBlocks/`. No Compose isso vira `context: .` mais `dockerfile: <caminho a partir da raiz>`.
- **Copiar os `.csproj` antes do resto do código**, rodar `dotnet restore`, e só então copiar `src/`. Enquanto as dependências não mudarem, o restore vem do cache de camadas.
- **`-p:OUROBOROS_SKIP_AUTOTEST=true` no `dotnet publish`**: o `Directory.Build.targets` dispara `dotnet test` da solution ao buildar a Api do Auth, e `tests/` não entra no contexto da imagem. Os testes rodam no build local e na CI, nunca no build da imagem.
- **`USER $APP_UID`**: o processo nunca roda como root. A variável vem da imagem base.
- **`curl` instalado no estágio de runtime**, só para servir ao `HEALTHCHECK` — a imagem de runtime não traz cliente HTTP.
- **`EXPOSE 8080`** e `HEALTHCHECK` apontando para o endpoint de prontidão do serviço.

Referências prontas: [Auth](../../../src/Services/Auth/Ouroboros.Services.Auth.Api/Dockerfile) e [Api Gateway](../../../src/ApiGateways/Ouroboros.ApiGateway/Dockerfile).

O `.dockerignore` na raiz mantém `bin/`, `obj/`, `.git/`, `.env`, `docker/secrets/`, `*.pem`, `tests/` e `docs/` fora do contexto de build. Ao criar uma pasta nova que não deva entrar na imagem, atualize-o.

## Docker Compose

Dois modos, e os dois precisam continuar funcionando:

| Comando | Sobe | Para quê |
|---|---|---|
| `docker compose up -d` | Só a infraestrutura (Postgres, Mailpit) | Dia a dia: as Apis rodam pela IDE contra essa infraestrutura |
| `docker compose --profile apps up -d --build` | Infraestrutura + todos os serviços | Exercitar a stack como ela seria em produção |

Regras:

- **Serviço de aplicação vai no profile `apps`.** Sem isso ele disputaria porta com o que estiver rodando pela IDE.
- **Infraestrutura fica fora do profile** (Postgres, Mailpit, e o que vier depois) — o fluxo pela IDE também depende dela.
- **Só o Api Gateway publica porta.** Serviço nenhum ganha `ports:`. Do host, um serviço só é alcançável através do gateway; é a fronteira de rede que torna a regra de isolamento real, e não só uma convenção de código.
- **Descoberta por nome de serviço** (`postgres`, `mailpit`, `auth-api`), nunca por IP.
- **`depends_on` sempre com `condition: service_healthy`**, nunca a forma curta. Sem isso o gateway sobe antes de existir alguém para quem encaminhar.
- **`restart: unless-stopped`** em todo serviço.
- **`container_name` no padrão `ouroboros-<serviço>`.**

## Portas

| Porta | Quem | Publicada no host? |
|---|---|---|
| 5432 | Postgres | Sim — migrations e DBeaver a partir da máquina |
| 1025 / 8025 | Mailpit (SMTP / interface web) | Sim |
| 5082 / 7272 | Api Gateway | Sim — **único** ponto de entrada público |
| 8080 | Qualquer serviço **dentro** do container | Não |
| 5081 / 7271 | Auth rodando pela IDE | Sim, só em desenvolvimento |

Dentro de container **todo serviço escuta em 8080** (`ASPNETCORE_HTTP_PORTS`); o que distingue um do outro é o nome na rede, não a porta. Fora de container, cada serviço novo pega o próximo par livre a partir de `5083`/`7273` no seu `launchSettings.json`.

## Configuração e segredos

Segredo nunca vai para `appsettings.json` — esse arquivo é versionado.

| Origem | Onde vale |
|---|---|
| **User Secrets** (`dotnet user-secrets`) | Desenvolvimento, rodando pela IDE |
| **Variável de ambiente** no `docker-compose.yml` | Container. `__` vira `:` na configuração do .NET (`ConnectionStrings__Postgres` → `ConnectionStrings:Postgres`) |
| **Secret do Compose** (arquivo montado em `/run/secrets/`) | Valores multilinha, como um PEM, que não cabem bem em variável de ambiente |
| **`.env`** na raiz | Valores que o próprio Compose interpola (senhas de banco). Nunca versionado; o modelo é o `.env.example` |

Quando a configuração puder vir de arquivo, a Api aceita as duas formas: o valor direto e um `<chave>Path` apontando para o arquivo — ver `ReadPem` no `Program.cs` do Auth. É isso que faz o mesmo código servir à IDE e ao container, sem `if` de ambiente.

Chaves e certificados ficam em `docker/secrets/`, que tem `.gitignore` próprio ignorando todo o seu conteúdo. **Nenhuma chave é versionada, em hipótese alguma.** Cada ambiente tem seu próprio par: um token emitido pela stack em container não vale na stack rodando pela IDE, e isso é o comportamento correto.

Ao adicionar uma variável nova que o desenvolvedor precise definir, documente-a no `.env.example` (comentada, se for opcional).

## Health checks

Toda Api expõe dois endpoints, ambos `[AllowAnonymous]` — quem os consulta (healthcheck do container, gateway, orquestrador) não tem como obter token:

- **`/health`** — *liveness*: o processo está de pé e respondendo? Nenhuma checagem de dependência entra aqui. Derrubar o container porque o banco piscou transforma uma falha em duas.
- **`/health/ready`** — *readiness*: o serviço consegue mesmo atender? Inclui banco e demais dependências, via `tags: ["ready"]`.

O `HEALTHCHECK` do container e o `depends_on` do Compose usam o **readiness**.

## Api Gateway

O gateway só roteia: sem regra de negócio, sem banco, sem `ProjectReference` a projeto de serviço. Ao adicionar um serviço:

- Uma rota por caminho no `appsettings.json`, com `ClusterId` próprio apontando para `http://<nome-do-servico>:8080`.
- O endereço do destino é **sobrescrito por variável de ambiente** no Compose (`ReverseProxy__Clusters__<cluster>__Destinations__<destino>__Address`), porque o `appsettings.json` aponta para `localhost`, que é o uso fora de container.
- **Rate limiting é responsabilidade do gateway**, não do serviço. Endpoint sensível a força bruta ou a disparo em massa (login, cadastro, recuperação de senha) recebe rota própria com a política estrita; o resto usa a padrão.
- **Correlation ID**: o gateway garante um `X-Correlation-Id` em todo request e o devolve na resposta. Serviço nenhum gera o seu — todos leem o que veio, inclusive o `GlobalExceptionHandler` ao registrar erro.
- **TLS termina no gateway.** Api de serviço não faz `UseHttpsRedirection` (o redirect devolveria `307` apontando para a porta interna, vazando a topologia); faz `UseForwardedHeaders` para continuar enxergando IP, scheme e host originais.

## Migrations

O Compose **não** aplica migrations. Elas são aplicadas a partir da máquina (`dotnet ef database update`), e é por isso que o Postgres publica a porta 5432. Conteúdo e nomenclatura das migrations seguem a [ags-dba](../ags-dba/SKILL.md).

## Comandos do dia a dia

```bash
docker compose up -d                          # infraestrutura (Postgres + Mailpit)
docker compose --profile apps up -d --build   # stack inteira; --build é obrigatório após mudar código
docker compose --profile apps ps              # status e saúde de cada container
docker compose --profile apps logs -f auth-api
docker compose --profile apps down            # derruba os containers (mantém o volume do banco)
docker compose down -v                        # derruba e apaga também os dados
```

A imagem carrega o binário publicado, não o código-fonte: sem `--build`, uma mudança em código não aparece no container.

Passo a passo completo de setup em [docs/0006 - Rodando a Stack em Containers](../../../docs/0006%20-%20Rodando%20a%20Stack%20em%20Containers.md).

## Evolução

Esta skill é o lugar para acumular, com o tempo, o que ainda não existe no projeto: pipeline de CI/CD, registry e versionamento de imagens, ambientes além do local, orquestração (Kubernetes ou similar) e observabilidade distribuída (OpenTelemetry e coletor).
