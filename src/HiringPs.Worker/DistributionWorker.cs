using HiringPs.Core;

namespace HiringPs.Worker;

// Telegram channel posts and IndexNow pings run apart from collection rounds and member delivery.
public sealed class DistributionWorker(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<DistributionWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        var nextIndexNow = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var tick = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                tick.CancelAfter(TimeSpan.FromMinutes(3));
                await using (var scope = scopes.CreateAsyncScope())
                {
                    var telegram = scope.ServiceProvider.GetRequiredService<TelegramChannelPublisher>();
                    await telegram.EnqueueAsync(tick.Token);
                    // Telegram allows about 20 channel posts per minute; stay well below it.
                    await telegram.DeliverAsync(5, TimeSpan.FromSeconds(4), tick.Token);
                }
                if (DateTimeOffset.UtcNow >= nextIndexNow)
                {
                    var minutes = Math.Clamp(configuration.GetValue("IndexNow:IntervalMinutes", 5), 1, 60);
                    nextIndexNow = DateTimeOffset.UtcNow.AddMinutes(minutes);
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IndexNowSubmitter>().RunAsync(tick.Token);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Do not log messages or URIs: the bot token is part of the Telegram endpoint.
                logger.LogWarning(
                    "Distribution background work failed ({ExceptionType}). Will retry.",
                    ex.GetType().Name
                );
            }
            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }
}
