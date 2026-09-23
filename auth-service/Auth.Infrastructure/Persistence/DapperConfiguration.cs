namespace Ouroboros.Auth.Infrastructure.Persistence;

using Dapper;

public static class DapperConfiguration
{
    // Permite mapear colunas snake_case (external_id) para propriedades PascalCase (ExternalId).
    public static void Configure()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}
