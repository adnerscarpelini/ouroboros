# Contexto de build é a raiz do repositório, pela mesma razão do Dockerfile do Auth
# (consistência entre os serviços), ainda que o gateway não referencie nenhum outro projeto.
# Uso: docker build -f src/ApiGateways/Ouroboros.ApiGateway/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.targets ./
COPY src/ApiGateways/Ouroboros.ApiGateway/*.csproj src/ApiGateways/Ouroboros.ApiGateway/

RUN dotnet restore src/ApiGateways/Ouroboros.ApiGateway/Ouroboros.ApiGateway.csproj

COPY src/ApiGateways/ src/ApiGateways/

RUN dotnet publish src/ApiGateways/Ouroboros.ApiGateway/Ouroboros.ApiGateway.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./

USER $APP_UID

EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=5 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Ouroboros.ApiGateway.dll"]
