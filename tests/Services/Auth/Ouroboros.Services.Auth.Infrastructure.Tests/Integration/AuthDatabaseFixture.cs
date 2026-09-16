using Npgsql;
using Ouroboros.BuildingBlocks.Infrastructure;
using Testcontainers.PostgreSql;

namespace Ouroboros.Services.Auth.Infrastructure.Tests.Integration;

// Sobe um Postgres efêmero (Testcontainers), aplica as migrations reais do serviço Auth com o
// mesmo MigrationRunner usado pelo Ouroboros.DatabaseMigrator, e mantém o container vivo durante
// toda a coleção de testes — subir o container é caro, então isso acontece uma única vez.
public sealed class AuthDatabaseFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
		.WithDatabase("ouroboros_auth")
		.WithUsername("auth_service")
		.WithPassword("auth_service")
		.Build();

	private NpgsqlConnectionFactory? _connectionFactory;

	public async Task InitializeAsync()
	{
		await _container.StartAsync();

		var migrations = MigrationLoader.Load(FindMigrationsDirectory());

		await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
		{
			await connection.OpenAsync();

			var runner = new MigrationRunner(connection);
			await runner.RunAsync(migrations, dryRun: false, CancellationToken.None);
		}

		_connectionFactory = new NpgsqlConnectionFactory(_container.GetConnectionString());
	}

	public DbSession CreateSession()
	{
		if (_connectionFactory is null)
		{
			throw new InvalidOperationException("A fixture ainda não foi inicializada.");
		}

		return new DbSession(_connectionFactory);
	}

	public async Task DisposeAsync()
	{
		if (_connectionFactory is not null)
		{
			await _connectionFactory.DisposeAsync();
		}

		await _container.DisposeAsync();
	}

	private static string FindMigrationsDirectory()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);

		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ouroboros.slnx")))
		{
			directory = directory.Parent;
		}

		if (directory is null)
		{
			throw new InvalidOperationException(
				"Não foi possível localizar a raiz do repositório (Ouroboros.slnx) a partir de "
				+ AppContext.BaseDirectory);
		}

		return Path.Combine(
			directory.FullName,
			"src",
			"Services",
			"Auth",
			"Ouroboros.Services.Auth.Infrastructure",
			"Migrations");
	}
}

[CollectionDefinition(Name)]
public sealed class AuthDatabaseCollection : ICollectionFixture<AuthDatabaseFixture>
{
	public const string Name = "Auth database";
}
