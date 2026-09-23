namespace Ouroboros.Auth.Api.Configuration;

using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.UseCases.ConfirmEmail;
using Ouroboros.Auth.Application.UseCases.RegisterUser;
using Ouroboros.Auth.Infrastructure.Persistence;
using Ouroboros.Auth.Infrastructure.Security;

public static class UseCaseConfiguration
{
    public static IServiceCollection AddUseCases(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();


        services.AddScoped<IUserRepository>(_ => new DapperUserRepository(connectionString));
        services.AddScoped<ITokenRepository>(_ => new DapperTokenRepository(connectionString));
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenGenerator, Sha256TokenGenerator>();
        services.AddScoped<IRegisterUserUseCase, RegisterUserInteractor>();
        services.AddScoped<IConfirmEmailUseCase, ConfirmEmailInteractor>();

        return services;
    }
}
