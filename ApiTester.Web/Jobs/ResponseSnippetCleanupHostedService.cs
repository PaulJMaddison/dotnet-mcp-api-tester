using Microsoft.Extensions.Options;

namespace ApiTester.Web.Jobs;

public sealed class ResponseSnippetCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<CleanupJobOptions> _options;
    private readonly ILogger<ResponseSnippetCleanupHostedService> _logger;

    public ResponseSnippetCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<CleanupJobOptions> options,
        ILogger<ResponseSnippetCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            _logger.LogInformation("Response snippet cleanup hosted service is disabled.");
            return;
        }

        var startupDelay = TimeSpan.FromSeconds(Math.Max(0, _options.Value.StartupDelaySeconds));
        if (startupDelay > TimeSpan.Zero)
            await Task.Delay(startupDelay, stoppingToken);

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.Value.SnippetTrimIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var coordinator = scope.ServiceProvider.GetRequiredService<RunCleanupCoordinator>();
                await coordinator.TrimLargeResponseSnippetsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Response snippet cleanup job failed; it will retry on next interval.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
