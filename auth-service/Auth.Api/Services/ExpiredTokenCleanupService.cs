namespace Ouroboros.Auth.Api.Services;

using Microsoft.Extensions.Options;
using Ouroboros.Auth.Api.Configuration;
using Ouroboros.Auth.Application.UseCases.CleanupExpiredTokens;

/// <summary>
/// Apaga periodicamente os tokens e refresh tokens expirados ha mais que a retencao (spec 2026092515). Roda no proprio
/// auth-service. Apagar expirados e idempotente: duas instancias rodando juntas nao corrompem nada, so repetem trabalho.
/// </summary>
public sealed class ExpiredTokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TokenCleanupSettings _settings;
    private readonly ILogger<ExpiredTokenCleanupService> _logger;

    public ExpiredTokenCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<TokenCleanupSettings> settings,
        ILogger<ExpiredTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Expired token cleanup is disabled");
            return;
        }

        using var timer = new PeriodicTimer(_settings.Interval);

        do
        {
            await RunCycleAsync(stoppingToken);
        }
        while (await WaitForNextCycleAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextCycleAsync(
        PeriodicTimer timer,
        CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var useCase = scope.ServiceProvider.GetRequiredService<ICleanupExpiredTokensUseCase>();

            var response = await useCase.ExecuteAsync(new CleanupExpiredTokensRequest(_settings.Retention), stoppingToken);

            // So contagens: nunca valores de token.
            _logger.LogInformation(
                "Expired token cleanup removed {Tokens} tokens and {RefreshTokens} refresh tokens",
                response.Tokens,
                response.RefreshTokens);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento do servico no meio do ciclo: nada a registrar.
        }
        catch (Exception e)
        {
            // O servico nao cai: o proximo ciclo tenta de novo.
            _logger.LogError(e, "Expired token cleanup failed; it will try again in the next cycle");
        }
    }
}
