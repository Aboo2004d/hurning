using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

public sealed class ToolMonitor(
    IServiceScopeFactory scopes,
    ToolWorkerRuntime runtime,
    IConfiguration configuration,
    ILogger<ToolMonitor> logger
) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(PulseLoopAsync(stoppingToken), CheckLoopAsync(stoppingToken));

    /// <summary>ينشر نبضات مستقلة للعامل اليدوي، أو لمستهلك الطلبات في الويب المحلي.</summary>
    public Task PulseAsync(CancellationToken ct) => PulseLoopAsync(ct);

    /// <summary>يحفظ لقطة واحدة للنبضة، بما فيها الحالة النهائية قبل خروج العامل المحلي.</summary>
    public async Task PublishStateAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HiringDb>();
        var state = await db.ToolWorkers.SingleOrDefaultAsync(x => x.Id == runtime.Id, ct);
        if (state is null)
        {
            state = new()
            {
                Id = runtime.Id,
                StartedAt = runtime.StartedAt,
                Host = Environment.MachineName.Truncate(200),
                Account = Environment.UserName.Truncate(200),
            };
            db.ToolWorkers.Add(state);
        }
        var snapshot = runtime.Snapshot;
        state.At = DateTimeOffset.UtcNow;
        state.Collecting = snapshot.Collecting;
        state.LastLoopAt = snapshot.LastLoopAt;
        state.LastSuccessAt = snapshot.LastSuccessAt;
        state.LastFailureAt = snapshot.LastFailureAt;
        state.ErrorType = snapshot.ErrorType;
        await db.SaveChangesAsync(ct);
    }

    private async Task PulseLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PublishStateAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Tool heartbeat failed ({Type}); check SQL/migrations.",
                    ex.GetType().Name
                );
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task CheckLoopAsync(CancellationToken ct)
    {
        // Start with the next midnight; restarting the worker never requests a check.
        var nextDailyCheck = ToolCheckSchedule.NextMidnight(DateTimeOffset.UtcNow);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<HiringDb>();
                var now = DateTimeOffset.UtcNow;
                if (ApplicationRuntime.Mode(configuration) != ApplicationRuntimeMode.Local && now >= nextDailyCheck)
                {
                    var handled = await ToolCheckQueue.EnqueueAsync(
                        db,
                        configuration,
                        ToolCheckKind.Standard,
                        "Worker.Midnight",
                        ct,
                        scheduledAt: nextDailyCheck
                    );
                    if (handled)
                        nextDailyCheck = ToolCheckSchedule.NextMidnight(now);
                }
                await ToolCheckQueue.RunNextAsync(
                    db,
                    configuration,
                    runtime,
                    scope.ServiceProvider,
                    ct
                );
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Tool checks failed ({Type}); retrying the queue.",
                    ex.GetType().Name
                );
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
