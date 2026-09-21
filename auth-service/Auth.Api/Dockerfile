FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY auth-service/Auth.Domain/Auth.Domain.csproj auth-service/Auth.Domain/
COPY auth-service/Auth.Application/Auth.Application.csproj auth-service/Auth.Application/
COPY auth-service/Auth.Infrastructure/Auth.Infrastructure.csproj auth-service/Auth.Infrastructure/
COPY auth-service/Auth.Api/Auth.Api.csproj auth-service/Auth.Api/
RUN dotnet restore auth-service/Auth.Api/Auth.Api.csproj

COPY auth-service/Auth.Domain/ auth-service/Auth.Domain/
COPY auth-service/Auth.Application/ auth-service/Auth.Application/
COPY auth-service/Auth.Infrastructure/ auth-service/Auth.Infrastructure/
COPY auth-service/Auth.Api/ auth-service/Auth.Api/

RUN dotnet publish auth-service/Auth.Api/Auth.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "Auth.Api.dll"]
