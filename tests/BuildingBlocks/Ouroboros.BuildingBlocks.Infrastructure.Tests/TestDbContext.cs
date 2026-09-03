using Microsoft.EntityFrameworkCore;
using Ouroboros.BuildingBlocks.Domain;

namespace Ouroboros.BuildingBlocks.Infrastructure.Tests;

// DbContext concreto só pra testar ErrorLogService/EmailQueueService contra a base AppDbContext,
// sem depender do DbContext de nenhum serviço real (BuildingBlocks não referencia serviços).
public sealed class TestDbContext : AppDbContext
{
	public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
	public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

	public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyCommonEntities();

		base.OnModelCreating(modelBuilder);
	}
}
