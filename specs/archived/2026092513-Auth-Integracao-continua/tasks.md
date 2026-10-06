# 2026092513 - Integracao continua — Tarefas

- [x] **Dev** — Criar `global.json` e ligar `-warnaserror` (o build atual já está sem warnings)
- [x] **Dev** — Criar `.github/workflows/ci.yml` com os jobs `build-test` e `publish-image` (`needs`, só em push na `master`), com permissões mínimas por job e actions fixadas por SHA; remover `docker-auth.yml`
- [x] **Dev** — Criar `.github/dependabot.yml` (nuget, github-actions e docker, semanal)
- [x] **Tester** — Incluir unitários e integração no job, com os `.trx` publicados como artefato; confirmar que um teste falhando impede a publicação
- [x] **Tech Writer** — Criar doc em `docs/project/` sobre o pipeline: etapas, como reproduzir localmente, publicação da imagem e o passo manual de branch protection
