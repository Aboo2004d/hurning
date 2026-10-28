// حلقة تشغيل عامل الجمع في الخلفية وتسجيل حالته.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>ينفذ طلبات الجمع اليدوية المعلقة فقط أثناء تشغيل الويب المحلي.</summary>
public class CollectionWorker(
    IServiceScopeFactory scopes,
    ILogger<CollectionWorker> logger,
    IConfiguration configuration,
    ToolWorkerRuntime runtime
) : BackgroundService
{
    /// <summary>ينتظر الطلبات اليدوية ويعزل فشل الجولة ويحترم إلغاء تشغيل الموقع.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var summary = await scope
                    .ServiceProvider.GetRequiredService<CollectionPipeline>()
                    .TryCollectQueuedAsync(stoppingToken);
                if (summary is not null)
                {
                    if (summary.FailedSources > 0)
                        runtime.Fail(new InvalidOperationException("ManualSourceFailures"));
                    else
                        runtime.Succeed();
                }
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
                    "Queued collection failed ({Type}); retry on the next poll. Check SQL/service configuration.",
                    ex.GetType().Name
                );
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue<int?>("Worker:PollSeconds") ?? 5, 2, 60)),
                    stoppingToken
                );
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
        runtime.Stop();
    }
}
