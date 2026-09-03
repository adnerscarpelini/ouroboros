using Microsoft.EntityFrameworkCore;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure;

// Mapeamento reutilizável por qualquer DbContext de serviço que queira persistir ErrorLog/EmailMessage
// na própria base — cada serviço chama isso no seu OnModelCreating e ganha sua própria cópia física
// dessas tabelas no schema "common" do seu banco. Código é compartilhado; dados não.
public static class CommonEntityConfiguration
{
	public static void ApplyCommonEntities(this ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<ErrorLog>().ToTable("error_logs", schema: "common");
		modelBuilder.Entity<EmailMessage>().ToTable("email_messages", schema: "common");
	}
}
