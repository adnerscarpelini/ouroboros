using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Ouroboros.BuildingBlocks.Infrastructure;

// Extraído do Ouroboros.DatabaseMigrator para ser compartilhado entre a ferramenta de linha de
// comando e os testes de integração, que precisam aplicar as mesmas migrations num Postgres efêmero.
public static class MigrationLoader
{
	private static readonly Regex FileNamePattern = new(
		@"^(?<version>\d{14})-(?<description>[A-Za-z0-9-]+)\.sql$",
		RegexOptions.CultureInvariant | RegexOptions.Compiled);

	public static IReadOnlyList<MigrationFile> Load(string directory)
	{
		if (!Directory.Exists(directory))
		{
			throw new DirectoryNotFoundException($"Diretório de migrations não encontrado: {directory}");
		}

		var files = Directory.GetFiles(directory, "*.sql", SearchOption.TopDirectoryOnly)
			.Select(ReadMigrationFile)
			.OrderBy(migration => migration.Version, StringComparer.Ordinal)
			.ToArray();

		var duplicateVersion = files
			.GroupBy(migration => migration.Version, StringComparer.Ordinal)
			.FirstOrDefault(group => group.Count() > 1);

		if (duplicateVersion is not null)
		{
			throw new InvalidOperationException(
				$"Prefixo de migration duplicado: '{duplicateVersion.Key}'.");
		}

		return files;
	}

	private static MigrationFile ReadMigrationFile(string path)
	{
		var fileName = Path.GetFileName(path);
		var match = FileNamePattern.Match(fileName);

		if (!match.Success)
		{
			throw new InvalidOperationException(
				$"Nome de migration inválido: '{fileName}'. " +
				"Esperado: AAAAMMDDHHMMSS-Descricao.sql.");
		}

		var sql = File.ReadAllText(path, Encoding.UTF8);
		var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));

		return new MigrationFile(
			match.Groups["version"].Value,
			match.Groups["description"].Value,
			path,
			sql,
			checksum);
	}
}
