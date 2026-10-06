# 2026092513 - Integracao continua

**Data:** 25/09/2026
**Servico(s):** auth-service (pipeline do monorepo)

## Solicitação

Na validação de maturidade do auth-service, o usuário aprovou uma verificação automática de build e testes. A revisão encontrou um workflow existente que publica a imagem sem rodar teste nenhum. A versão anterior desta spec dizia que não havia pipeline, o que estava errado.

## Análise

Estado atual: `.github/workflows/docker-auth.yml` faz build e push da imagem para o GHCR (tags `latest` e `sha`) a cada push na `master`, sem build .NET, sem teste e com actions fixadas só por tag de versão. Uma mudança quebrada vira `latest` na hora. Referências: GitHub Actions security hardening (permissões mínimas e actions fixadas por SHA) e OpenSSF Scorecard.

Decisões:
1. **Um único workflow, `.github/workflows/ci.yml`**, que substitui o `docker-auth.yml`. Gatilhos: `pull_request` e `push` na `master`.
   - O job `build-test` roda nos dois casos.
   - O job `publish-image` depende dele (`needs: build-test`) e só roda em push na `master`.
   - Sem teste verde, não há imagem.
2. **`global.json`** fixa o SDK .NET 10 usado localmente, e o `actions/setup-dotnet` lê dele.
3. **Etapas do `build-test`:**
   1. restore com cache do NuGet pelo hash dos `.csproj`;
   2. build em Release;
   3. testes unitários;
   4. testes de integração (2026092512; os runners `ubuntu-latest` têm Docker);
   5. resultados `.trx` publicados como artefato.
4. **Warnings como erro** (`-warnaserror`). O Dev zera os warnings atuais antes de ligar.
5. **Formatação:** um `.editorconfig` com o estilo já usado no código e `dotnet format --verify-no-changes`.
6. **Dependências:**
   - `dotnet list package --vulnerable --include-transitive` falha o pipeline se encontrar vulnerabilidade;
   - Dependabot semanal para `nuget`, `github-actions` e `docker`.
7. **Imagem:** o `publish-image` roda um scan da imagem com Trivy antes do push e falha em vulnerabilidade `CRITICAL` ou `HIGH` que tenha correção.
8. **Segurança do pipeline:**
   - `permissions: contents: read` no nível do workflow, e `packages: write` só no job de publicação;
   - actions de terceiros fixadas por SHA;
   - nenhum segredo além do `GITHUB_TOKEN`.
9. **Proteção da branch.** Exigir o check `build-test` para o merge é uma configuração do GitHub, que o usuário faz. O Tech Writer documenta o passo a passo.
