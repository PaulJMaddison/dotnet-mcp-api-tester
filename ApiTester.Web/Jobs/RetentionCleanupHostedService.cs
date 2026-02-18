using Microsoft.Extensions.Options;

namespace ApiTester.Web.Jobs;

public sealed class RetentionCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<CleanupJobOptions> _options;
    private readonly ILogger<RetentionCleanupHostedService> _logger;

    public RetentionCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<CleanupJobOptions> options,
        ILogger<RetentionCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            _logger.LogInformation("Retention cleanup hosted service is disabled.");
            return;
        }

        var startupDelay = TimeSpan.FromSeconds(Math.Max(0, _options.Value.StartupDelaySeconds));
        if (startupDelay > TimeSpan.Zero)
            await Task.Delay(startupDelay, stoppingToken);

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.Value.RetentionIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var coordinator = scope.ServiceProvider.GetRequiredService<RunCleanupCoordinator>();
                await coordinator.PruneByRetentionPlanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retention cleanup job failed; it will retry on next interval.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
