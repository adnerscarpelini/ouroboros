# 0006 - Rodando a Stack em Containers

Este documento é o passo a passo prático. As **convenções** por trás dele (o que todo `Dockerfile` precisa ter, o que entra no Compose ao criar um serviço, política de portas e de segredos) estão na skill [ags-devops](../.claude/skills/ags-devops/SKILL.md).

## Os dois modos

| Comando | O que sobe | Quando usar |
|---|---|---|
| `docker compose up -d` | Postgres + Mailpit | Dia a dia: as Apis rodam pela IDE ou por `dotnet run`, contra o banco em container. É o fluxo do [0002](0002%20-%20Setup%20do%20Banco%20de%20Dados%20Local.md). |
| `docker compose --profile apps up -d` | Postgres + Mailpit + Auth + Api Gateway | Quando quiser exercitar a stack como ela seria em produção: cada serviço no seu container, se achando pela rede. |

O Mailpit (servidor SMTP de desenvolvimento, ver [0007](0007%20-%20Fila%20de%20E-mails%20%28Outbox%29.md)) sobe nos dois modos, porque o fluxo pela IDE também precisa dele.

Os serviços de aplicação ficam no profile `apps` para que o primeiro modo continue funcionando sem disputar as portas com o que estiver rodando pela IDE.

## Preparação (uma vez)

### 1. Arquivo `.env`

Mesmo `.env` do [0002](0002%20-%20Setup%20do%20Banco%20de%20Dados%20Local.md) — o container do Auth monta a connection string a partir de `AUTH_DB_PASSWORD`.

### 2. Par de chaves RSA para os containers

Fora do container, as chaves do JWT ficam no User Secrets. Dentro, elas chegam como **secret do Compose**: arquivos montados em `/run/secrets/`, porque um PEM é multilinha e não cabe bem numa variável de ambiente.

```bash
openssl genrsa -out docker/secrets/jwt-private.pem 2048
openssl rsa -in docker/secrets/jwt-private.pem -pubout -out docker/secrets/jwt-public.pem
```

Este é um par **próprio do ambiente containerizado**, diferente do que está no seu User Secrets — ambientes diferentes não compartilham chave de assinatura. Na prática: um token emitido rodando pela IDE não vale na stack em container, e vice-versa. É o comportamento correto.

A pasta `docker/secrets/` tem um `.gitignore` que ignora todo o seu conteúdo. Nenhuma chave é versionada.

### 3. Migrations

O Compose não aplica migrations. O banco continua sendo migrado a partir da máquina, como no [0002](0002%20-%20Setup%20do%20Banco%20de%20Dados%20Local.md) — a porta `5432` é publicada justamente para isso:

```bash
cd src/Services/Auth/Ouroboros.Services.Auth.Infrastructure
dotnet ef database update --startup-project ../Ouroboros.Services.Auth.Api --context AuthDbContext
```

## Comandos do dia a dia

```bash
docker compose --profile apps up -d --build   # sobe tudo, reconstruindo as imagens
docker compose --profile apps ps              # status e saúde de cada container
docker compose --profile apps logs -f auth-api
docker compose --profile apps down            # derruba os containers (mantém o volume do banco)
```

O `--build` é necessário sempre que o código mudar: a imagem carrega o binário publicado, não o código-fonte.

## Como cada serviço recebe configuração

Nenhum segredo está no `appsettings.json`. Dentro do container, tudo chega por variável de ambiente (o `__` vira `:` na configuração do .NET) ou por secret montado:

| Configuração | Como chega |
|---|---|
| `ConnectionStrings:Postgres` | Variável `ConnectionStrings__Postgres`, montada no `docker-compose.yml` a partir de `AUTH_DB_PASSWORD` |
| `App:PublicBaseUrl` | Variável `App__PublicBaseUrl` (padrão: `http://localhost:5082`) |
| `Jwt:SigningKeyPem` / `Jwt:PublicKeyPem` | Secret montado; a Api lê o caminho em `Jwt:SigningKeyPemPath` / `Jwt:PublicKeyPemPath` |
| Destino do gateway | Variável `ReverseProxy__Clusters__auth-cluster__Destinations__auth-api__Address` |

A Api aceita a chave das duas formas: valor direto na configuração (User Secrets, fora do container) ou caminho de arquivo em `<chave>Path` (secret, dentro do container).

## Rede e portas

- **Só o Api Gateway publica porta** (`5082`). O Auth escuta em `8080` apenas dentro da rede do Compose — do host, `localhost:5081` não responde. É a promessa do [0000](0000%20-%20Arquitetura.md#api-gateway) valendo de fato, não só no papel.
- Os serviços se acham pelo **nome do serviço** no Compose (`postgres`, `auth-api`), não por IP.
- O Postgres continua publicando `5432` para as migrations e o DBeaver.

## Health checks

Auth e Gateway expõem `GET /health` (anônimo). O Compose usa isso para ordenar a subida:

- `auth-api` só sobe depois do Postgres estar `healthy`;
- `api-gateway` só sobe depois do `auth-api` estar `healthy`.

Sem isso, o gateway subiria antes de existir alguém para quem encaminhar.

## Detalhes das imagens

- Build em dois estágios: compila no `sdk:10.0`, publica só o resultado no `aspnet:10.0`.
- O contexto de build é a **raiz do repositório**, porque os serviços referenciam projetos de `src/BuildingBlocks/`.
- Os `.csproj` são copiados antes do resto do código para que o `restore` fique em cache enquanto as dependências não mudarem.
- O processo roda como usuário **não-root** (`APP_UID`, da imagem base).
- O `publish` passa `-p:OUROBOROS_SKIP_AUTOTEST=true`: o `Directory.Build.targets` dispara `dotnet test` da solution ao buildar a Api do Auth, e a pasta `tests/` não entra no contexto da imagem. Os testes rodam no build local, não no build da imagem.
