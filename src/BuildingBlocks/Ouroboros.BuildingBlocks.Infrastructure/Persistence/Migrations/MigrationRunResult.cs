namespace Ouroboros.BuildingBlocks.Infrastructure;

public sealed record MigrationRunResult(
	int AppliedCount,
	IReadOnlyList<MigrationFile> Pending)
{
	public int PendingCount => Pending.Count;
}
