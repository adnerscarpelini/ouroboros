# Integração Contínua

Um único workflow, `.github/workflows/ci.yml`, roda o build e os testes do auth-service e publica a imagem. Ele substitui o antigo `docker-auth.yml`, que publicava a imagem sem rodar teste nenhum: uma mudança quebrada virava `latest` na hora.

## Gatilhos e jobs

| Job | Quando roda | O que faz |
|---|---|---|
| `build-test` | Em todo `pull_request` e em todo `push` na `master` | Restore, build, checagem de vulnerabilidades e testes |
| `publish-image` | **Só** em `push` na `master`, e só depois do `build-test` | Build e push da imagem no GHCR |

O `publish-image` tem `needs: build-test`: **sem teste verde, não há imagem**. Em pull request a imagem nunca é publicada.

## Etapas do `build-test`

1. **Restore**, com cache do NuGet (`~/.nuget/packages`) pela chave `hashFiles('**/*.csproj')`.
2. **Build em Release com warnings como erro**: `dotnet build Ouroboros.slnx --configuration Release --no-restore -warnaserror`. O build está sem warnings e assim continua.
3. **Vulnerabilidades**: `dotnet list Ouroboros.slnx package --vulnerable --include-transitive`. O comando sai com código 0 mesmo com vulnerabilidade, então o passo falha pelo texto do relatório (`has the following vulnerable packages`).
4. **Testes unitários** (`Auth.Domain.Tests` e `Auth.Application.Tests`).
5. **Testes de integração** (`Auth.Integration.Tests`). Os runners `ubuntu-latest` têm Docker, e os testes sobem um SQL Server descartável com Testcontainers (ver `docs/project/0004 - Testes de Integracao.md`).
6. **Resultados `.trx`** publicados como artefato `test-results`, mesmo quando um teste falha.

O SDK vem do `global.json` (.NET 10, `rollForward: latestFeature`), o mesmo que se usa localmente.

## Reproduzir localmente

Na raiz do repositório, com o Docker rodando (os testes de integração precisam dele):

```
dotnet restore Ouroboros.slnx
dotnet build Ouroboros.slnx --configuration Release --no-restore -warnaserror
dotnet list Ouroboros.slnx package --vulnerable --include-transitive
dotnet test test/auth-service/Auth.Domain.Tests --configuration Release --no-build
dotnet test test/auth-service/Auth.Application.Tests --configuration Release --no-build
dotnet test test/auth-service/Auth.Integration.Tests --configuration Release --no-build
```

No relatório de vulnerabilidades, cada projeto deve dizer `has no vulnerable packages`. Para validar o próprio workflow sem enviá-lo ao GitHub, rode o `actionlint`:

```
docker run --rm -v "$PWD:/repo" -w /repo rhysd/actionlint:latest
```

## Publicação da imagem

- Imagem: `ghcr.io/<dono>/ouroboros-auth`, construída de `auth-service/Auth.Api/Dockerfile`.
- Tags: `latest` e `sha-<commit>` (`docker/metadata-action`).
- A imagem roda sem root (ver `docs/project/0002 - Docker.md`).
- O `publish-image` é o único job com `packages: write`.

## Segurança do pipeline

- `permissions: contents: read` no nível do workflow. `packages: write` só no job de publicação.
- **Actions de terceiros fixadas por SHA de commit**, com a versão no comentário da linha. Uma tag de versão pode ser movida; o SHA, não.
- Nenhum segredo além do `GITHUB_TOKEN`.
- O **Dependabot** (`.github/dependabot.yml`) abre pull requests semanais para `nuget`, `github-actions` e `docker`. Ao aceitar uma atualização de action, ele troca o SHA e o comentário de versão.

## Passo manual: proteger a branch `master`

Exigir o check `build-test` para o merge é uma configuração do GitHub, e **quem faz é o dono do repositório**:

1. No repositório, abra **Settings → Branches** (ou **Settings → Rules → Rulesets**) e crie uma regra para `master`.
2. Marque **Require status checks to pass before merging** e escolha o check **`Build and test`** (nome do job `build-test`). O check só aparece na lista depois de rodar ao menos uma vez.
3. Marque **Require branches to be up to date before merging**.
4. Marque **Require a pull request before merging** (ou, se o fluxo for push direto na `master`, saiba que o push direto não passa por esse check: a imagem `latest` só é publicada se o `build-test` desse commit passar, mas o código já está na `master`).

Sem essa regra, o `build-test` roda e avisa, mas não impede o merge.

## O que ficou de fora

- **`dotnet format --verify-no-changes` com `.editorconfig`:** reformatar o código existente e barrar o pipeline por estilo geraria ruído sem proteger nada. Volta em spec própria se o time crescer.
- **Scan da imagem com Trivy bloqueando o pipeline:** falharia por CVE da imagem base sem relação com a mudança. Pode voltar como relatório sem bloqueio, em spec própria.
