using Microsoft.EntityFrameworkCore;
using Ouroboros.BuildingBlocks.Domain;
using Ouroboros.BuildingBlocks.Infrastructure;
using Ouroboros.Services.Notifications.Domain;

namespace Ouroboros.Services.Notifications.Infrastructure;

public sealed class NotificationsDbContext : AppDbContext
{
	public DbSet<EmailDelivery> EmailDeliveries => Set<EmailDelivery>();
	public DbSet<EmailDeliveryAttempt> EmailDeliveryAttempts => Set<EmailDeliveryAttempt>();
	public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
	public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

	public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.HasDefaultSchema("notifications");

		modelBuilder.Entity<EmailDelivery>(builder =>
		{
			// A restrição que absorve a reentrega de um transporte at-least-once: a mesma solicitação
			// chegando duas vezes esbarra no banco, não na sorte de duas leituras não se cruzarem.
			builder.HasIndex(x => new { x.Producer, x.RequestId }).IsUnique();

			// O índice que o dispatcher usa a cada rodada: só as entregas cuja hora já chegou.
			builder.HasIndex(x => new { x.Status, x.NextAttemptAt });

			builder.HasMany(x => x.Attempts)
				.WithOne(x => x.Delivery)
				.HasForeignKey(x => x.EmailDeliveryId);
		});

		modelBuilder.Entity<EmailDeliveryAttempt>(builder =>
		{
			builder.HasIndex(x => new { x.EmailDeliveryId, x.AttemptNumber }).IsUnique();
		});

		modelBuilder.ApplyCommonEntities();

		base.OnModelCreating(modelBuilder);
	}
}
