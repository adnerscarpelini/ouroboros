namespace Ouroboros.Auth.Api.Configuration;

using Microsoft.Extensions.Options;
using Ouroboros.Auth.Application.Gateways;
using Ouroboros.Auth.Application.Settings;
using Ouroboros.Auth.Application.UseCases.ConfirmEmail;
using Ouroboros.Auth.Application.UseCases.DeleteUser;
using Ouroboros.Auth.Application.UseCases.GetUser;
using Ouroboros.Auth.Application.UseCases.Login;
using Ouroboros.Auth.Application.UseCases.Logout;
using Ouroboros.Auth.Application.UseCases.LogoutAll;
using Ouroboros.Auth.Application.UseCases.RefreshAccessToken;
using Ouroboros.Auth.Application.UseCases.RegisterUser;
using Ouroboros.Auth.Application.UseCases.RequestPasswordReset;
using Ouroboros.Auth.Application.UseCases.ResetPassword;
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

        services.AddSingleton<IJwtTokenGenerator>(provider =>
            new JwtTokenGenerator(provider.GetRequiredService<IOptions<JwtSettings>>().Value));
        services.AddSingleton(provider =>
            new RefreshTokenSettings(TimeSpan.FromDays(provider.GetRequiredService<IOptions<JwtSettings>>().Value.RefreshTokenExpirationDays)));

        services.AddSingleton(new SqlConnectionFactory(connectionString));
        services.AddScoped<DbSession>();
        services.AddScoped<IUnitOfWork, SqlUnitOfWork>();
        services.AddScoped<IUserRepository, DapperUserRepository>();
        services.AddScoped<ITokenRepository, DapperTokenRepository>();
        services.AddScoped<IRefreshTokenRepository, DapperRefreshTokenRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenGenerator, Sha256TokenGenerator>();
        services.AddHttpClient<IBreachedPasswordChecker, PwnedPasswordsChecker>(client =>
        {
            client.BaseAddress = PwnedPasswordsChecker.BaseAddress;
            client.Timeout = PwnedPasswordsChecker.Timeout;
        });
        services.AddScoped<IRegisterUserUseCase, RegisterUserInteractor>();
        services.AddScoped<IConfirmEmailUseCase, ConfirmEmailInteractor>();
        services.AddScoped<ILoginUseCase, LoginInteractor>();
        services.AddScoped<IRefreshAccessTokenUseCase, RefreshAccessTokenInteractor>();
        services.AddScoped<ILogoutUseCase, LogoutInteractor>();
        services.AddScoped<ILogoutAllUseCase, LogoutAllInteractor>();
        services.AddScoped<IRequestPasswordResetUseCase, RequestPasswordResetInteractor>();
        services.AddScoped<IResetPasswordUseCase, ResetPasswordInteractor>();
        services.AddScoped<IGetUserUseCase, GetUserInteractor>();
        services.AddScoped<IDeleteUserUseCase, DeleteUserInteractor>();

        return services;
    }
}
