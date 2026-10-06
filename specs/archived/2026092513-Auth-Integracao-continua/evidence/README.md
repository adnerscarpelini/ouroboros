# Evidências

Esta spec entrega um pipeline do GitHub Actions, que só roda de verdade no GitHub. O que dá para provar aqui é: (1) o workflow é válido e seguro (linter e conferência dos SHAs), (2) os comandos de cada passo funcionam na ordem do workflow, e (3) um teste falhando interrompe o job antes da publicação. A execução no GitHub (e o passo manual de proteção de branch) fica para depois do push.

## Execução

- Comandos dos passos do `build-test`, reproduzidos localmente por `ci-local.sh`, na ordem e com os mesmos argumentos do `ci.yml` (`set -e` faz o papel do GitHub Actions: o primeiro passo que falha interrompe o job).
- Data: 06/10/2026
- Resultado verde: `ci-local-green.txt`. Restore, build em Release com `-warnaserror` (0 avisos, 0 erros), relatório de vulnerabilidades (todos os projetos sem pacote vulnerável) e os três conjuntos de testes. 579 testes, 579 aprovados.

| Projeto | Total | Passou | Falhou |
|---|---|---|---|
| Auth.Domain.Tests | 61 | 61 | 0 |
| Auth.Application.Tests | 225 | 225 | 0 |
| Auth.Integration.Tests | 293 | 293 | 0 |

Os `.trx` foram gerados em `TestResults/` (`domain.trx`, `application.trx` e `integration.trx`), que é o que o passo `Upload test results` publica como artefato.

## Rastreabilidade

### Decisões da spec

| Decisão | Verificação | Resultado |
|---|---|---|
| 1. Um único workflow `ci.yml` que substitui o `docker-auth.yml`; gatilhos `pull_request` e `push` na `master`; `publish-image` com `needs: build-test` e só em push na `master` | `.github/workflows/docker-auth.yml` removido. `actionlint` (imagem `rhysd/actionlint:latest`) valida o workflow sem nenhum aviso (saída `0`). O `publish-image` declara `needs: build-test` e `if: github.event_name == 'push' && github.ref == 'refs/heads/master'` | Passou |
| 2. `global.json` fixa o SDK .NET 10 e o `setup-dotnet` lê dele | `global.json` (`10.0.100`, `rollForward: latestFeature`); `ci.yml` usa `global-json-file: global.json`; `dotnet --version` local resolve `10.0.401` | Passou |
| 3. Etapas: restore com cache pelo hash dos `.csproj`, build em Release, testes unitários, testes de integração, `.trx` como artefato | `ci-local-green.txt` mostra cada etapa na ordem; os três `.trx` existem em `TestResults/`; `ci.yml` tem os passos `Cache NuGet packages` (`hashFiles('**/*.csproj')`) e `Upload test results` (`if: ${{ !cancelled() }}`) | Passou |
| 4. Warnings como erro | `dotnet build ... -warnaserror` com 0 avisos e 0 erros. A conferência já pegou um aviso (`CS8604` num teste de auditoria), corrigido antes de fechar a spec | Passou |
| 5. `.editorconfig` e `dotnet format` descartados | só documentado | Não se aplica |
| 6. Falha do pipeline em vulnerabilidade; Dependabot semanal para `nuget`, `github-actions` e `docker` | `ci-local-green.txt`: relatório sem vulnerabilidade (`has no vulnerable packages` em todos os projetos); `.github/dependabot.yml` com os três ecossistemas, `interval: weekly` | Passou |
| 6. O passo falha pelo texto `has the following vulnerable packages` (o `dotnet list` sai com 0 mesmo com vulnerabilidade) | só inspeção do script do passo. Não forcei uma dependência vulnerável | Passou (inspeção) |
| 7. Trivy bloqueante descartado | só documentado | Não se aplica |
| 8. `permissions: contents: read` no workflow, `packages: write` só na publicação, actions por SHA, nenhum segredo além do `GITHUB_TOKEN` | `ci.yml`; os 9 `uses:` do arquivo são fixados por SHA de 40 caracteres, e cada SHA foi conferido na API do GitHub (`gh api repos/<action>/commits/<sha>`, os 8 repositórios existem). Único segredo: `secrets.GITHUB_TOKEN` | Passou |
| 9. Proteção de branch é configuração do GitHub, documentada passo a passo | `docs/project/0006 - Integracao Continua.md`, seção "Passo manual: proteger a branch `master`" | Entregue (a configuração é do dono do repositório) |

### Tarefa Dev

| Critério | Verificação | Resultado |
|---|---|---|
| `global.json` e `-warnaserror` | `global.json`; build em `ci-local-green.txt` | Passou |
| `ci.yml` com `build-test` e `publish-image`, permissões mínimas por job, actions por SHA; `docker-auth.yml` removido | `actionlint` sem avisos; conferência dos SHAs | Passou |
| `dependabot.yml` (nuget, github-actions, docker, semanal) | `.github/dependabot.yml` | Passou |

### Tarefa Tester

| Critério | Verificação | Resultado |
|---|---|---|
| Unitários e integração no job, com os `.trx` publicados como artefato | `ci-local-green.txt`; `ci.yml` (passos `Unit tests (Domain)`, `Unit tests (Application)`, `Integration tests (API and database)` e `Upload test results`) | Passou |
| Um teste falhando impede a publicação | `ci-local-red.txt`: com um teste que falha de propósito (arquivo temporário, não commitado), o passo `Unit tests (Domain)` sai com código `1`, e as etapas seguintes (testes de aplicação e de integração e a linha `publish-image would run here`) **não rodam**. No workflow, o `publish-image` depende do `build-test` (`needs`), que só termina com sucesso se todos os passos passarem | Passou |

### Tarefa Tech Writer

| Critério | Artefato | Resultado |
|---|---|---|
| Doc do pipeline: etapas, como reproduzir localmente, publicação da imagem e o passo manual de branch protection | `docs/project/0006 - Integracao Continua.md` (vinculado no `Ouroboros.slnx`) | Entregue |

## Artefatos brutos

- `ci-local.sh`: script que reproduz os passos do job.
- `ci-local-green.txt`: execução com todos os testes passando.
- `ci-local-red.txt`: execução com um teste falhando de propósito.

## Observações

- **Não houve execução no GitHub Actions.** O que valida o pipeline de fato é o primeiro push (e um pull request). O `actionlint` e a reprodução local cobrem a sintaxe, a lógica de dependência entre jobs e os comandos, mas não o comportamento dos runners (cache, artefato, login no GHCR).
- **Versões das actions.** Os SHAs fixados são os das últimas versões publicadas em 06/10/2026 (`actions/checkout` v7.0.1, `actions/setup-dotnet` v6.0.0, `actions/cache` v6.1.0, `actions/upload-artifact` v7.0.1, `docker/setup-buildx-action` v4.4.1, `docker/login-action` v4.6.0, `docker/metadata-action` v6.2.0 e `docker/build-push-action` v7.4.0). As versões antigas do `docker-auth.yml` (`@v3`, `@v4`...) eram só tags móveis.
- **`.gitignore`.** O relatório `vulnerable-packages.txt`, que o passo de vulnerabilidades cria na raiz, entrou no `.gitignore`. A pasta `TestResults/` já era ignorada.
- **Vinculação à solution.** `global.json` ficou em `Solution Items`, e `.github/` ganhou as pastas virtuais `/.github/` e `/.github/workflows/`.
- **Aviso na suíte de testes.** A verificação `-warnaserror` encontrou e fez corrigir um aviso de nulidade em `AuditTrailApiTests` (da spec 2026092519), que o build normal não mostrava como erro.
