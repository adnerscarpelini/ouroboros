# Contexto de build é a raiz do repositório, não a pasta deste projeto: o serviço referencia
# projetos de src/BuildingBlocks/, que precisam entrar na imagem junto.
# Uso: docker build -f src/Services/Auth/Ouroboros.Services.Auth.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Só os .csproj primeiro: enquanto nenhum deles mudar, o restore vem do cache de camadas
# e uma alteração em código não obriga a baixar os pacotes de novo.
COPY Directory.Build.targets ./
COPY src/BuildingBlocks/Ouroboros.BuildingBlocks.Domain/*.csproj src/BuildingBlocks/Ouroboros.BuildingBlocks.Domain/
COPY src/BuildingBlocks/Ouroboros.BuildingBlocks.Application/*.csproj src/BuildingBlocks/Ouroboros.BuildingBlocks.Application/
COPY src/BuildingBlocks/Ouroboros.BuildingBlocks.Infrastructure/*.csproj src/BuildingBlocks/Ouroboros.BuildingBlocks.Infrastructure/
COPY src/Services/Auth/Ouroboros.Services.Auth.Domain/*.csproj src/Services/Auth/Ouroboros.Services.Auth.Domain/
COPY src/Services/Auth/Ouroboros.Services.Auth.Application/*.csproj src/Services/Auth/Ouroboros.Services.Auth.Application/
COPY src/Services/Auth/Ouroboros.Services.Auth.Infrastructure/*.csproj src/Services/Auth/Ouroboros.Services.Auth.Infrastructure/
COPY src/Services/Auth/Ouroboros.Services.Auth.Api/*.csproj src/Services/Auth/Ouroboros.Services.Auth.Api/

RUN dotnet restore src/Services/Auth/Ouroboros.Services.Auth.Api/Ouroboros.Services.Auth.Api.csproj

COPY src/ src/

# OUROBOROS_SKIP_AUTOTEST: o Directory.Build.targets dispara "dotnet test" da solution inteira ao
# buildar esta Api, e a pasta tests/ não entra no contexto da imagem (ver .dockerignore).
# Os testes rodam no build local e na CI, não aqui.
RUN dotnet publish src/Services/Auth/Ouroboros.Services.Auth.Api/Ouroboros.Services.Auth.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:OUROBOROS_SKIP_AUTOTEST=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl serve ao HEALTHCHECK abaixo — a imagem de runtime não traz nenhum cliente HTTP.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./

# APP_UID vem da imagem base: o processo não roda como root.
USER $APP_UID

EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=5 \
    CMD curl --fail --silent http://localhost:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "Ouroboros.Services.Auth.Api.dll"]
