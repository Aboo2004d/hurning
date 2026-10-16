using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

internal static class ToolSqlChecks
{
    public static async Task RunAsync(
        HiringDb db,
        IConfiguration configuration,
        IServiceProvider services
    )
    {
        var count = 0;
        void Check(bool value, string label)
        {
            if (!value)
                throw new Exception(label);
            count++;
            Console.WriteLine("PASS " + label);
        }
        Check(
            await ToolCheckQueue.EnqueueAsync(
                db,
                configuration,
                ToolCheckKind.Standard,
                "Checks",
                default
            ),
            "standard tool check is durably queued"
        );
        Check(
            !await ToolCheckQueue.EnqueueAsync(
                db,
                configuration,
                ToolCheckKind.Standard,
                "Checks",
                default
            ),
            "duplicate active tool check is rejected"
        );
        Check(
            await ToolCheckQueue.EnqueueAsync(
                db,
                configuration,
                ToolCheckKind.AiLive,
                "Checks",
                default
            ),
            "explicit AI request has its own queue slot"
        );
        var live = await db.ToolCheckRequests.SingleAsync(x => x.Kind == ToolCheckKind.AiLive);
        live.Status = ToolRequestStatus.Running;
        live.StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        await db.SaveChangesAsync();
        var beforeAi = await db.AiUsage.CountAsync();
        await ToolCheckQueue.RunNextAsync(
            db,
            configuration,
            new ToolWorkerRuntime(),
            services,
            default
        );
        db.ChangeTracker.Clear();
        live = await db.ToolCheckRequests.SingleAsync(x => x.Id == live.Id);
        Check(
            live.Status == ToolRequestStatus.Failed
                && live.ActiveSlot is null
                && live.Outcome == "Interrupted",
            "interrupted live AI is released without an automatic API retry"
        );
        Check(await db.AiUsage.CountAsync() == beforeAi, "standard diagnostics do not invoke AI");
        var standard = await db.ToolCheckRequests.SingleAsync(x =>
            x.Kind == ToolCheckKind.Standard
        );
        Check(
            standard.Status == ToolRequestStatus.Completed && standard.ActiveSlot is null,
            "worker persists completion and releases the active slot"
        );
        Check(
            await db.ToolCheckResults.CountAsync(x => x.RequestId == standard.Id)
                == ToolMonitoringRules.StandardTools.Length,
            "each tool persists its own diagnostic result"
        );
        live.RequestedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        await db.SaveChangesAsync();
        var opportunitiesBefore = await db.Opportunities.CountAsync();
        Check(
            await ToolCheckQueue.EnqueueAsync(
                db,
                configuration,
                ToolCheckKind.AiLive,
                "Checks",
                default
            ),
            "explicit live test can be requested after cooldown"
        );
        await ToolCheckQueue.RunNextAsync(
            db,
            configuration,
            new ToolWorkerRuntime(),
            services,
            default
        );
        Check(
            await db.ToolCheckResults.AnyAsync(x =>
                x.Tool == "AiLive" && x.State == ToolState.Ready
            ),
            "mocked provider response is saved as live-test evidence"
        );
        Check(
            await db.AiUsage.CountAsync() == beforeAi + 1
                && await db.Opportunities.CountAsync() == opportunitiesBefore,
            "live test records usage without publishing any opportunity"
        );
        await using var pipelineLock = new SqlRunLock(configuration.GetConnectionString("Hiring")!);
        await using var toolsLock = new SqlRunLock(
            configuration.GetConnectionString("Hiring")!,
            "HiringPs.Tools.Runner"
        );
        Check(
            await pipelineLock.AcquireAsync(default) && await toolsLock.AcquireAsync(default),
            "tool and collection locks do not block each other"
        );
        await pipelineLock.VerifyAsync(default);
        await toolsLock.VerifyAsync(default);
        Console.WriteLine($"{count} tool SQL checks passed.");
    }
}
