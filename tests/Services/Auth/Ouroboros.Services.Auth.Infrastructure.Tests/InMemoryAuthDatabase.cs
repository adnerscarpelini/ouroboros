using Microsoft.EntityFrameworkCore;

namespace Ouroboros.Services.Auth.Infrastructure.Tests;

// Cada instância representa um banco em memória isolado. Entrega contextos novos apontando pro mesmo
// banco de propósito: ler num contexto diferente do que gravou é o que faz o teste exercitar o Include
// de verdade, em vez de achar as navegações já resolvidas pelo change tracker.
internal sealed class InMemoryAuthDatabase
{
	private readonly string _databaseName = Guid.NewGuid().ToString();

	public AuthDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<AuthDbContext>()
			.UseInMemoryDatabase(_databaseName)
			.Options;

		var dbContext = new AuthDbContext(options);
		dbContext.Database.EnsureCreated();

		return dbContext;
	}
}
