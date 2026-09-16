using Npgsql;
using Ouroboros.BuildingBlocks.Infrastructure;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
	PrintUsage();
	return args.Length == 0 ? 1 : 0;
}

var service = args[0].ToLowerInvariant();
var dryRun = args.Skip(1).Any(argument => argument is "--dry-run" or "-d");
var connectionString = GetOption(args, "--connection-string")
	?? GetEnvironmentConnectionString(service)
	?? throw new InvalidOperationException(
		$"Connection string não configurada. Use --connection-string ou a variável " +
		$"OUROBOROS_{service.ToUpperInvariant()}_CONNECTION_STRING.");

var repositoryRoot = FindRepositoryRoot();
var migrationsDirectory = service switch
{
	"auth" => Path.Combine(
		repositoryRoot,
		"src",
		"Services",
		"AuthService",
		"Ouroboros.AuthService.Infrastructure",
		"Migrations"),

	"notifications" => Path.Combine(
		repositoryRoot,
		"src",
		"Services",
		"NotificationsService",
		"Ouroboros.NotificationsService.Infrastructure",
		"Migrations"),
	_ => throw new InvalidOperationException(
		$"Serviço '{service}' inválido. Use 'auth' ou 'notifications'.")
};

var migrations = MigrationLoader.Load(migrationsDirectory);
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

var runner = new MigrationRunner(connection);
var result = await runner.RunAsync(migrations, dryRun, CancellationToken.None);

if (dryRun)
{
	foreach (var migration in result.Pending)
	{
		Console.WriteLine($"Pendente: {Path.GetFileName(migration.Path)}");
	}

	Console.WriteLine($"{result.PendingCount} migration(s) pendente(s).");
}
else
{
	Console.WriteLine($"{result.AppliedCount} migration(s) aplicada(s).");
}

return 0;

static string? GetEnvironmentConnectionString(string service)
{
	return Environment.GetEnvironmentVariable(
		$"OUROBOROS_{service.ToUpperInvariant()}_CONNECTION_STRING")
		?? Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
}

static string? GetOption(string[] arguments, string optionName)
{
	var index = Array.IndexOf(arguments, optionName);

	return index >= 0 && index + 1 < arguments.Length
		? arguments[index + 1]
		: null;
}

static string FindRepositoryRoot()
{
	var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

	while (directory is not null)
	{
		if (File.Exists(Path.Combine(directory.FullName, "Ouroboros.slnx")))
		{
			return directory.FullName;
		}

		directory = directory.Parent;
	}

	throw new InvalidOperationException(
		"Não foi possível localizar a raiz do repositório (Ouroboros.slnx).");
}

static void PrintUsage()
{
	Console.WriteLine("Uso: dotnet run -- <auth|notifications> [--dry-run] [--connection-string <valor>]");
	Console.WriteLine("Variáveis aceitas: OUROBOROS_AUTH_CONNECTION_STRING ou OUROBOROS_NOTIFICATIONS_CONNECTION_STRING");
}
