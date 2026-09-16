namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed record MigrationFile(
	string Version,
	string Description,
	string Path,
	string Sql,
	string Checksum);
