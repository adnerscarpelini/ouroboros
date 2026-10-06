#!/bin/bash
# Reproduz localmente os passos do job build-test do .github/workflows/ci.yml, na mesma ordem e com os mesmos comandos.
# "set -e" faz o papel do GitHub Actions: o primeiro passo que falha interrompe o job.
set -e
cd /c/Users/adner/source/repos/ouroboros
export DOTNET_CLI_UI_LANGUAGE=en
rm -rf TestResults

echo "### Restore";            dotnet restore Ouroboros.slnx
echo "### Build";              dotnet build Ouroboros.slnx --configuration Release --no-restore -warnaserror
echo "### Check vulnerable packages"
dotnet list Ouroboros.slnx package --vulnerable --include-transitive 2>&1 | tee vulnerable-packages.txt
if grep -q "has the following vulnerable packages" vulnerable-packages.txt; then echo "::error::Vulnerable NuGet packages found"; exit 1; fi
echo "### Unit tests (Domain)"
dotnet test test/auth-service/Auth.Domain.Tests --configuration Release --no-build --logger "trx;LogFileName=domain.trx" --results-directory TestResults
echo "### Unit tests (Application)"
dotnet test test/auth-service/Auth.Application.Tests --configuration Release --no-build --logger "trx;LogFileName=application.trx" --results-directory TestResults
echo "### Integration tests (API and database)"
dotnet test test/auth-service/Auth.Integration.Tests --configuration Release --no-build --logger "trx;LogFileName=integration.trx" --results-directory TestResults
echo "### build-test OK"
echo "### publish-image would run here (needs: build-test)"
