namespace Ouroboros.Auth.Api.Configuration;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.UseCases.RegisterUser;
using Ouroboros.Auth.Infrastructure.Persistence;
using Ouroboros.Auth.Infrastructure.Security;

public static class UseCaseConfiguration
{
    public static IServiceCollection AddUseCases(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddScoped<IUserRepository>(_ => new DapperUserRepository(connectionString));
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IRegisterUserUseCase, RegisterUserInteractor>();

        return services;
    }
}
