using Microsoft.Extensions.Options;

namespace LinkPulse.Api.Expiration;

public sealed class ExpiredLinkCleanupWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<ExpiredLinkCleanupOptions> options,
    TimeProvider timeProvider,
    ILogger<ExpiredLinkCleanupWorker> logger)
    : BackgroundService
{
    private readonly TimeSpan interval =
        TimeSpan.FromSeconds(
            options.Value.IntervalSeconds);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Expired link cleanup worker started.");

        try
        {
            await RunCleanupAsync(stoppingToken);

            using var timer =
                new PeriodicTimer(interval);

            while (await timer.WaitForNextTickAsync(
                       stoppingToken))
            {
                await RunCleanupAsync(
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Expired link cleanup worker stopped.");
        }
    }

    private async Task RunCleanupAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope =
                serviceScopeFactory
                    .CreateAsyncScope();

            var processor =
                scope.ServiceProvider
                    .GetRequiredService<
                        IExpiredLinkProcessor>();

            var processedCount =
                await processor.ProcessAsync(
                    timeProvider.GetUtcNow(),
                    cancellationToken);

            if (processedCount > 0)
            {
                logger.LogInformation(
                    "Expired link cleanup deactivated {ProcessedCount} links.",
                    processedCount);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Expired link cleanup failed.");
        }
    }
}