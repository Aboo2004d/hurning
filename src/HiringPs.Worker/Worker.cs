// حلقة تشغيل عامل الجمع في الخلفية وتسجيل حالته.
using HiringPs.Core;

namespace HiringPs.Worker;

public class Worker(
    IServiceScopeFactory scopes,
    ILogger<Worker> logger,
    IConfiguration configuration,
    ToolWorkerRuntime runtime
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                runtime.Begin();
                await using var scope = scopes.CreateAsyncScope();
                await scope
                    .ServiceProvider.GetRequiredService<CollectionPipeline>()
                    .TickAsync(stoppingToken);
                runtime.Succeed();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                await OperationFailureRecorder.TryRecordAsync(scopes, OperationArea.Collection, "Pipeline", ex, stoppingToken);
                runtime.Fail(ex);
                logger.LogError(
                    "Pipeline failed ({Type}); retry in one minute. Check SQL/service configuration.",
                    ex.GetType().Name
                );
            }

            await Task.Delay(
                TimeSpan.FromSeconds(
                    Math.Clamp(configuration.GetValue<int?>("Worker:PollSeconds") ?? 5, 2, 60)
                ),
                stoppingToken
            );
        }
    }
}
