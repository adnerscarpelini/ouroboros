# 0002 - Setup do Banco de Dados Local

> Este documento cobre o fluxo do dia a dia: **só o banco em container**, com as Apis rodando pela IDE.
> Para subir a stack inteira em container (Postgres + Auth + Gateway), ver [0006 - Rodando a Stack em Containers](0006%20-%20Rodando%20a%20Stack%20em%20Containers.md).

Passo a passo para subir o PostgreSQL local do projeto do zero — útil se formatar a máquina ou configurar um ambiente novo. Assume que o repositório já está clonado e que a máquina é Windows.

## 1. Instalar o WSL2

1. Abrir o **PowerShell como Administrador**.
2. Rodar:
   ```powershell
   wsl --install
   ```
3. Reiniciar o Windows quando for pedido.
4. Depois de reiniciar, o Ubuntu abre sozinho e pede pra criar um usuário/senha Linux — pode ser qualquer coisa, é só local.

## 2. Instalar o Docker Desktop

1. Baixar em: https://www.docker.com/products/docker-desktop/
2. Rodar o instalador, deixando marcada a opção **"Use WSL 2 instead of Hyper-V"** (já vem assim por padrão nas versões atuais).
3. Abrir o Docker Desktop e esperar o ícone da baleia (bandeja do Windows) mostrar "Docker Desktop is running".
4. Confirmar num terminal comum (não precisa ser admin):
   ```powershell
   docker --version
   ```

## 3. Criar o arquivo `.env` local

As senhas do banco **não ficam versionadas no Git** — só existem no arquivo `.env`, que é local e ignorado pelo `.gitignore`. O que vai pro repositório é o `.env.example`, que é só um modelo com valores de exemplo (`change-me`), não as senhas de verdade.

O `.env` tem duas senhas, não uma só — a instância Postgres é compartilhada entre serviços (ver [docs/0000 - Arquitetura.md](0000%20-%20Arquitetura.md#banco-de-dados)), mas cada serviço tem sua própria credencial de banco:

- `POSTGRES_PASSWORD`: senha do superusuário administrativo (`postgres`) — usado só pra gestão da instância (DBeaver como admin, scripts de init). A Api nunca conecta com ele.
- `AUTH_DB_PASSWORD`: senha da role `auth_service`, dona do banco `ouroboros_auth` — é essa que a Api do Auth usa.
- `NOTIFICATIONS_DB_PASSWORD`: senha da role `notifications_service`, dona do banco `ouroboros_notifications` — é essa que a Api de Notificações usa.

1. Na raiz do projeto, copiar o modelo:
   ```powershell
   Copy-Item .env.example .env
   ```
2. Abrir o `.env` e trocar os dois `change-me` por senhas de verdade (quaisquer uma, é ambiente local).

Se a máquina foi formatada e o `.env` antigo se perdeu, isso é esperado — é só criar um novo `.env` com senhas novas. Como os dados do banco ficam num volume Docker (não no `.env`), só o container em si precisa ser recriado; se o volume também tiver sido perdido (ex.: reinstalou o Docker do zero), o banco sobe vazio de novo — nesse caso é preciso reaplicar as migrations SQL com o runner (ver seção 6) e atualizar a connection string no runner com a nova senha.

## 4. Subir o banco

Na raiz do projeto:

```bash
docker compose up -d
```

Na primeira subida (volume novo), os scripts em `docker/postgres/init/` rodam automaticamente e criam um banco por serviço, cada um com a sua role já dona dele — `ouroboros_auth`/`auth_service` e `ouroboros_notifications`/`notifications_service`. Não precisa criar nada manualmente, e um serviço novo entra como mais um script, sem precisar de um container a mais.

Cada script também revoga `CONNECT` de `PUBLIC` no banco que criou. Sem isso, o PostgreSQL deixaria a role de um serviço se conectar ao banco do outro — o isolamento entre serviços depende disso, não só da convenção.

> **Volume que já existe.** O entrypoint de init só roda em volume novo: um script novo não é executado só porque apareceu no diretório. Como este é um ambiente de desenvolvimento, o caminho mais simples é recriar o volume:
>
> ```bash
> docker compose down -v
> docker compose up -d
> ```
>
> Isso apaga os dados de todos os bancos. Para preservá-los, rode o SQL do script novo à mão, conectado como superusuário.

Conferir se subiu e está saudável:

```bash
docker ps --filter "name=ouroboros-postgres"
```

O `STATUS` deve mostrar `healthy` depois de alguns segundos.

## 5. Configurar a connection string do .NET (User Secrets)

A senha também não pode ir pro `appsettings.json` (esse arquivo é versionado). O equivalente ao `.env` do lado do .NET é o **User Secrets** — guarda a connection string com a senha de verdade fora do repositório, associada só à sua máquina.

1. Inicializar (só precisa uma vez, já feito no projeto, mas fica documentado caso o `UserSecretsId` do `.csproj` mude):
   ```bash
   dotnet user-secrets init --project src/Services/Auth/Ouroboros.Services.Auth.Api
   ```
2. Definir a connection string, usando a senha de `AUTH_DB_PASSWORD` no `.env` (não a de `POSTGRES_PASSWORD` — a Api conecta como `auth_service`, não como superusuário):
   ```bash
   dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=ouroboros_auth;Username=auth_service;Password=<AUTH_DB_PASSWORD do .env>" --project src/Services/Auth/Ouroboros.Services.Auth.Api
   ```

Sem isso, a Api lança erro ao iniciar (`Connection string 'Postgres' não configurada`).

O serviço de Notificações tem o seu próprio cofre e a sua própria credencial — ele conecta como `notifications_service`, no banco dele, e não alcança o banco do Auth:

```bash
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=ouroboros_notifications;Username=notifications_service;Password=<NOTIFICATIONS_DB_PASSWORD do .env>" --project src/Services/Notifications/Ouroboros.Services.Notifications.Api
```

Também pelo User Secrets: o par de chaves RSA usado para assinar (JWT) e validar os tokens emitidos no login. É um par assimétrico, não uma senha única — a chave **privada** assina e só o Auth a possui; a chave **pública** só valida, e é o que qualquer outro serviço vai precisar quando existir (ver [docs/0000 - Arquitetura.md](0000%20-%20Arquitetura.md#autenticação-entre-serviços)).

1. Gerar o par de chaves (formato PEM, com `openssl` — já vem com o Git for Windows):
   ```bash
   openssl genrsa -out jwt-private.pem 2048
   openssl rsa -in jwt-private.pem -pubout -out jwt-public.pem
   ```
2. Guardar as duas no User Secrets (o conteúdo do `.pem`, arquivo inteiro, como uma única string):
   ```bash
   dotnet user-secrets set "Jwt:SigningKeyPem" "$(cat jwt-private.pem)" --project src/Services/Auth/Ouroboros.Services.Auth.Api
   dotnet user-secrets set "Jwt:PublicKeyPem" "$(cat jwt-public.pem)" --project src/Services/Auth/Ouroboros.Services.Auth.Api
   ```
3. Apagar os dois arquivos `.pem` da pasta do projeto depois de guardados no User Secrets — eles não devem ficar soltos no disco fora do cofre do User Secrets, e principalmente nunca devem ser commitados.

Sem isso, a Api lança erro ao iniciar (`Configuração 'Jwt:SigningKeyPem' não definida` ou `'Jwt:PublicKeyPem' não definida`).

## 6. Aplicar migrations SQL

As migrations ficam em cada `Infrastructure/Migrations` e são aplicadas pelo projeto administrativo `Ouroboros.DatabaseMigrator`. O runner usa a tabela `public.schema_migrations`, valida checksum e executa cada arquivo em uma transação. O Compose não aplica migrations automaticamente.

Defina a connection string fora do repositório. No PowerShell, por exemplo:

```powershell
$env:OUROBOROS_AUTH_CONNECTION_STRING = "Host=localhost;Port=5432;Database=ouroboros_auth;Username=auth_service;Password=<AUTH_DB_PASSWORD do .env>"
$env:OUROBOROS_NOTIFICATIONS_CONNECTION_STRING = "Host=localhost;Port=5432;Database=ouroboros_notifications;Username=notifications_service;Password=<NOTIFICATIONS_DB_PASSWORD do .env>"
```

Confira primeiro o que será executado:

```powershell
dotnet run --project src/Tools/Ouroboros.DatabaseMigrator -- auth --dry-run
dotnet run --project src/Tools/Ouroboros.DatabaseMigrator -- notifications --dry-run
```

Depois aplique cada banco separadamente:

```powershell
dotnet run --project src/Tools/Ouroboros.DatabaseMigrator -- auth
dotnet run --project src/Tools/Ouroboros.DatabaseMigrator -- notifications
```

O runner não deve ser executado com a senha do superusuário `postgres`: cada serviço aplica suas próprias migrations usando sua role (`auth_service` ou `notifications_service`).

## 7. Instalar o DBeaver e conectar

1. Baixar o **DBeaver Community**: https://dbeaver.io/download/
2. Criar uma nova conexão PostgreSQL com:
   - **Host**: `localhost`
   - **Porta**: `5432`
   - **Banco/Database**: `ouroboros_auth`
   - **Usuário**: `auth_service`
   - **Senha**: o valor de `AUTH_DB_PASSWORD` no `.env`

   (Pra tarefas administrativas na instância — não específicas de um serviço — conecte como superusuário: usuário `postgres` / senha de `POSTGRES_PASSWORD` no `.env`, banco `postgres`.)

## Comandos do dia a dia

```bash
docker compose up -d              # sobe o banco
docker compose down               # derruba o container (mantém os dados no volume)
docker compose down -v            # derruba e apaga também os dados (começa do zero)
docker compose logs -f postgres   # acompanha os logs
```
