using Microsoft.EntityFrameworkCore;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure;

// Mapeamento reutilizável por qualquer DbContext de serviço que queira persistir ErrorLog/OutboxMessage
// na própria base — cada serviço chama isso no seu OnModelCreating e ganha sua própria cópia física
// dessas tabelas no schema "common" do seu banco. Código é compartilhado; dados não.
public static class CommonEntityConfiguration
{
	public static void ApplyCommonEntities(this ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ErrorLog>().ToTable("error_logs", schema: "common");

		modelBuilder.Entity<OutboxMessage>(builder =>
		{
			builder.ToTable("outbox_messages", schema: "common");

			// O índice que o publisher usa a cada rodada: só as pendentes cuja espera já venceu.
			builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
		});
	}
}
