using System.Data.Common;

namespace Ouroboros.BuildingBlocks.Application;

public interface IDbConnectionFactory
{
	Task<DbConnection> OpenAsync(CancellationToken cancellationToken);
}
