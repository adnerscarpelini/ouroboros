namespace Ouroboros.Auth.Integration.Tests.Persistence;

using System.Text.RegularExpressions;
using Xunit;

// Regra de hints da skill ouroboros-dba: todo SELECT dos repositorios leva WITH (NOLOCK) ou WITH (READPAST) em cada tabela
// do FROM e dos JOINs; UPDATE, DELETE e INSERT nao levam hint. Varre o SQL escrito nos repositorios Dapper.
public sealed class SqlHintConventionTests
{
    private static readonly Regex RawSqlLiteral = new("\"\"\"(.*?)\"\"\"", RegexOptions.Singleline);

    private static readonly Regex TableReference = new(
        @"\b(?:FROM|JOIN)\s+(?<table>auth\.\w+)(?:\s+AS\s+\w+)?(?<hint>\s+WITH\s*\(\s*(?:NOLOCK|READPAST)\s*\))?",
        RegexOptions.IgnoreCase);

    [Fact]
    public void EveryTableInEverySelectShouldHaveNolockOrReadpast()
    {
        var (selects, _) = ReadRepositorySql();
        var offenders = new List<string>();

        foreach (var (file, sql) in selects)
        {
            foreach (Match reference in TableReference.Matches(sql))
            {
                if (!reference.Groups["hint"].Success)
                {
                    offenders.Add($"{file}: {reference.Groups["table"].Value} sem WITH (NOLOCK) ou WITH (READPAST)");
                }
            }
        }

        // Protege contra passar em vazio se o formato do SQL mudar e a varredura deixar de achar os SELECTs.
        Assert.True(selects.Count >= 8, $"Esperava ao menos 8 SELECTs nos repositorios, achei {selects.Count}.");
        Assert.Empty(offenders);
    }

    [Fact]
    public void NoWriteStatementShouldCarryNolockOrReadpast()
    {
        var (_, writes) = ReadRepositorySql();
        var offenders = writes
            .Where(item => Regex.IsMatch(item.Sql, @"\b(NOLOCK|READPAST)\b", RegexOptions.IgnoreCase))
            .Select(item => $"{item.File}: comando de escrita com hint")
            .ToList();

        Assert.True(writes.Count >= 8, $"Esperava ao menos 8 comandos de escrita nos repositorios, achei {writes.Count}.");
        Assert.Empty(offenders);
    }

    private static (List<(string File, string Sql)> Selects, List<(string File, string Sql)> Writes) ReadRepositorySql()
    {
        var directory = Path.Combine(FindRepositoryRoot(), "auth-service", "Auth.Infrastructure", "Persistence");
        var selects = new List<(string, string)>();
        var writes = new List<(string, string)>();

        foreach (var file in Directory.GetFiles(directory, "Dapper*Repository.cs"))
        {
            var text = File.ReadAllText(file);

            foreach (Match literal in RawSqlLiteral.Matches(text))
            {
                var sql = literal.Groups[1].Value;
                var name = Path.GetFileName(file);

                if (Regex.IsMatch(sql.TrimStart(), "^SELECT", RegexOptions.IgnoreCase))
                {
                    selects.Add((name, sql));
                }
                else if (Regex.IsMatch(sql.TrimStart(), "^(INSERT|UPDATE|DELETE)", RegexOptions.IgnoreCase))
                {
                    writes.Add((name, sql));
                }
            }
        }

        return (selects, writes);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ouroboros.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Ouroboros.slnx not found above the test output directory.");
    }
}
