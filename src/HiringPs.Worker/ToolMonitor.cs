using HiringPs.Core;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Worker;

public sealed class ToolMonitor(
    IServiceScopeFactory scopes,
    ToolWorkerRuntime runtime,
    IConfiguration configuration,
    ILogger<ToolMonitor> logger
) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(PulseLoopAsync(stoppingToken), CheckLoopAsync(stoppingToken));

    private async Task PulseLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
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
                if (now >= nextDailyCheck)
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
