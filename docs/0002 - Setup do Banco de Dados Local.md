# 0002 - Setup do Banco de Dados Local

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

A senha do banco **não fica versionada no Git** — só existe no arquivo `.env`, que é local e ignorado pelo `.gitignore`. O que vai pro repositório é o `.env.example`, que é só um modelo com um valor de exemplo (`change-me`), não a senha de verdade.

1. Na raiz do projeto, copiar o modelo:
   ```powershell
   Copy-Item .env.example .env
   ```
2. Abrir o `.env` e trocar `change-me` por uma senha de verdade (qualquer uma, é ambiente local).

Se a máquina foi formatada e o `.env` antigo se perdeu, isso é esperado — é só criar um novo `.env` com uma senha nova. Como os dados do banco ficam num volume Docker (não no `.env`), só o container em si precisa ser recriado; se o volume também tiver sido perdido (ex.: reinstalou o Docker do zero), o banco sobe vazio de novo.

## 4. Subir o banco

Na raiz do projeto:

```bash
docker compose up -d
```

Conferir se subiu e está saudável:

```bash
docker ps --filter "name=ouroboros-postgres"
```

O `STATUS` deve mostrar `healthy` depois de alguns segundos.

## 5. Instalar o DBeaver e conectar

1. Baixar o **DBeaver Community**: https://dbeaver.io/download/
2. Criar uma nova conexão PostgreSQL com:
   - **Host**: `localhost`
   - **Porta**: `5432`
   - **Banco/Database**: `ouroboros`
   - **Usuário**: o valor de `POSTGRES_USER` no `.env`
   - **Senha**: o valor de `POSTGRES_PASSWORD` no `.env`

## Comandos do dia a dia

```bash
docker compose up -d              # sobe o banco
docker compose down               # derruba o container (mantém os dados no volume)
docker compose down -v            # derruba e apaga também os dados (começa do zero)
docker compose logs -f postgres   # acompanha os logs
```
