using Ouroboros.BuildingBlocks.Application;

namespace Ouroboros.BuildingBlocks.Infrastructure.Tests;

public sealed class FakeCorrelationIdAccessor : ICorrelationIdAccessor
{
	public string? CorrelationId { get; set; }

	public string? GetCorrelationId()
	{
		return CorrelationId;
	}
}
