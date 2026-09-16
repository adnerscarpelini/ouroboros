namespace Ouroboros.BuildingBlocks.Infrastructure.Tests.Migrations;

public sealed class MigrationLoaderTests : IDisposable
{
	private readonly string _directory;

	public MigrationLoaderTests()
	{
		_directory = Directory.CreateTempSubdirectory("ouroboros-migrations-tests").FullName;
	}

	public void Dispose()
	{
		Directory.Delete(_directory, recursive: true);
	}

	[Fact]
	public void Load_returns_migrations_ordered_by_version()
	{
		WriteMigration("20260101000000-CreateA.sql", "select 1;");
		WriteMigration("20260103000000-CreateC.sql", "select 3;");
		WriteMigration("20260102000000-CreateB.sql", "select 2;");

		var migrations = MigrationLoader.Load(_directory);

		Assert.Equal(3, migrations.Count);
		Assert.Equal(["20260101000000", "20260102000000", "20260103000000"], migrations.Select(migration => migration.Version));
	}

	[Fact]
	public void Load_computes_a_stable_checksum_for_the_same_content()
	{
		WriteMigration("20260101000000-CreateA.sql", "select 1;");

		var first = MigrationLoader.Load(_directory).Single();
		var second = MigrationLoader.Load(_directory).Single();

		Assert.Equal(first.Checksum, second.Checksum);
		Assert.NotEmpty(first.Checksum);
	}

	[Fact]
	public void Load_throws_when_two_migrations_share_the_same_version_prefix()
	{
		WriteMigration("20260101000000-CreateA.sql", "select 1;");
		WriteMigration("20260101000000-CreateB.sql", "select 2;");

		Assert.Throws<InvalidOperationException>(() => MigrationLoader.Load(_directory));
	}

	[Fact]
	public void Load_throws_when_a_file_name_does_not_match_the_expected_pattern()
	{
		WriteMigration("CreateWithoutTimestamp.sql", "select 1;");

		Assert.Throws<InvalidOperationException>(() => MigrationLoader.Load(_directory));
	}

	[Fact]
	public void Load_throws_when_the_directory_does_not_exist()
	{
		var missingDirectory = Path.Combine(_directory, "nao-existe");

		Assert.Throws<DirectoryNotFoundException>(() => MigrationLoader.Load(missingDirectory));
	}

	private void WriteMigration(string fileName, string sql)
	{
		File.WriteAllText(Path.Combine(_directory, fileName), sql);
	}
}
