// استخراج البيانات بالنموذج، ثم التحقق من الأدلة وتحديد النشر أو المراجعة أو الأرشفة.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>مكون CollectionPipeline ضمن مجال Collection؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public partial class CollectionPipeline(
    HiringDb db,
    BrowserCollector browser,
    OpportunityService opportunities,
    IConfiguration config,
    ILogger<CollectionPipeline> log,
    IServiceScopeFactory scopes
)
{
    private static int zoneWarningLogged;

    /// <summary>ينفذ دورة العمل مع القفل والنبضات وعزل فشل المصدر أو العنصر.</summary>
    public async Task TickAsync(CancellationToken ct)
    {
        await using var gate = new SqlRunLock(config.GetConnectionString("Hiring")!);
        if (!await gate.AcquireAsync(ct))
            return;
        _ = TextRules.PalestineTimeZone; // resolve once so a fallback warning is known
        if (
            TextRules.PalestineTimeZoneWarning is { } zoneWarning
            && Interlocked.Exchange(ref zoneWarningLogged, 1) == 0
        )
            log.LogWarning("{Warning}", zoneWarning);
        var heartbeat = await db.Heartbeats.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (heartbeat is null)
        {
            heartbeat = new();
            db.Heartbeats.Add(heartbeat);
        }

        heartbeat.At = DateTimeOffset.UtcNow;
        heartbeat.State = "Running";
        await db.SaveChangesAsync(ct);
        var finalState = "Failed";
        try
        {
            await RunAsync(gate, ct);
            finalState = "Idle";
        }
        finally
        {
            // A failed save must never leave the heartbeat claiming "Running".
            await FinishHeartbeatAsync(heartbeat.Id, finalState);
        }
    }

    /// <summary>يسجل نهاية دورة الجمع بنطاق مستقل حتى عند فشل حفظ الدورة.</summary>
    private async Task FinishHeartbeatAsync(int id, string state)
    {
        try
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var scope = scopes.CreateAsyncScope();
            var heartbeatDb = scope.ServiceProvider.GetRequiredService<HiringDb>();
            await heartbeatDb
                .Heartbeats.Where(x => x.Id == id)
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.At, DateTimeOffset.UtcNow)
                            .SetProperty(x => x.State, state),
                    budget.Token
                );
        }
        catch (Exception ex)
        {
            log.LogWarning("Heartbeat update failed ({Type}).", ex.GetType().Name);
        }
    }

    /// <summary>ينفذ عمل الدورة أو الإخطار وفق الإعدادات والطابور دون تغيير عقود المزود.</summary>
    private async Task RunAsync(SqlRunLock gate, CancellationToken ct)
    {
        var activePrompt = await PromptWorkflow.ActivateAsync(db, ct);
        await RetentionCleanup.RunAsync(db, config, ct);
        var now = DateTimeOffset.UtcNow;
        var requests = db.Set<SourceCollectionRequest>();
        await requests
            .Where(x =>
                (x.Status == "Pending" || x.Status == "Running")
                && (!x.Source.Enabled || !x.Source.UsageApproved)
            )
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, "Cancelled")
                        .SetProperty(x => x.Error, "Source disabled or usage approval removed.")
                        .SetProperty(x => x.FinishedAt, (DateTimeOffset?)now),
                ct
            );
        var manual = await requests
            .Include(x => x.Source)
            .Where(x => x.Status == "Pending" || x.Status == "Running")
            .OrderBy(x => x.RequestedAt)
            .FirstOrDefaultAsync(ct);
        if (manual is not null)
        {
            manual.Status = "Running";
            manual.StartedAt ??= now;
            await db.SaveChangesAsync(ct);
        }

        var selectedSources = manual is not null
            ? (manual.DiscoveryFinished ? new List<Source>() : new List<Source> { manual.Source })
            : await db
                .Sources.Where(x =>
                    x.Enabled
                    && x.UsageApproved
                    && (
                        x.CollectionRetryAt != null
                            ? x.CollectionRetryAt <= now
                            : x.NextDueAt <= now
                    )
                )
                .OrderBy(x => x.NextDueAt)
                .Take(3)
                .ToListAsync(ct);
        foreach (var source in selectedSources)
            await new SourceDiscoveryService(db, browser, config, log, scopes)
                .DiscoverAsync(gate, source, manual, activePrompt, ct);

        await ProcessPendingAsync(gate, manual, activePrompt, ct);

        if (manual is not null && manual.Status != "Failed")
        {
            var remaining = await db.SourceItems.AnyAsync(
                x =>
                    x.SourceId == manual.SourceId
                    && (
                        x.Status == ProcessingStatus.Discovered
                        || (
                            x.Status == ProcessingStatus.Retry
                            && (x.RetryAt == null || x.RetryAt <= DateTimeOffset.UtcNow)
                        )
                    ),
                ct
            );
            if (!remaining)
            {
                manual.Status = "Completed";
                manual.FinishedAt = DateTimeOffset.UtcNow;
                var counts = await db
                    .SourceItems.Where(x => x.SourceId == manual.SourceId)
                    .GroupBy(x => x.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync(ct);
                manual.Error =
                    "Discovery round finished; source item totals: "
                    + string.Join(", ", counts.Select(x => $"{x.Status}: {x.Count}"))
                    + ". Pending AI/retries are not published successes.";
            }
        }

        await opportunities.ArchiveExpiredAsync(ct, manual?.SourceId);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>يرتب العناصر المؤهلة ويمرر حالة إيقاف AI بين معالجاتها.</summary>
    private async Task<(int Count, long LastId, bool Stop)> ProcessPendingAsync(
        SqlRunLock gate,
        SourceCollectionRequest? manual,
        PromptVersion activePrompt,
        CancellationToken ct,
        long? afterId = null,
        long? maximumId = null
    )
    {
        var now = DateTimeOffset.UtcNow;
        var aiPausedUntil = await AiGovernor.PausedUntilAsync(db, now, ct);
        if (aiPausedUntil is null && await AiGovernor.BudgetExceededAsync(db, config, now, ct))
            aiPausedUntil = await PauseForBudgetAsync(now, ct);
        var pendingQuery = db
            .SourceItems.AsNoTracking()
            .Where(x =>
                x.Source.Enabled
                && x.Source.UsageApproved
                && (
                    x.Status == ProcessingStatus.Discovered
                    || x.Status == ProcessingStatus.AwaitingAi
                    || x.Status == ProcessingStatus.Retry
                )
                && (x.RetryAt == null || x.RetryAt <= now)
            );
        // While AI is paused, items still get fetched and stored, but nothing waits only on AI.
        if (aiPausedUntil is not null)
            pendingQuery = pendingQuery.Where(x => x.Status != ProcessingStatus.AwaitingAi);
        if (manual is not null)
            pendingQuery = pendingQuery.Where(x => x.SourceId == manual.SourceId);
        if (afterId is not null) pendingQuery = pendingQuery.Where(x => x.Id > afterId && x.Id <= maximumId);
        // Manual rounds visit every eligible ID at most once, even if its state remains AwaitingAi.
        var ordered = afterId is not null ? pendingQuery.OrderBy(x => x.Id)
            : pendingQuery.OrderBy(x => x.ExtractionPromptVersionId == null ? 0 : 1).ThenBy(x => x.FirstSeenAt);
        var pending = await ordered
            .Select(x => x.Id)
            .Take(15)
            .ToListAsync(ct);
        var count = 0;
        var lastId = afterId ?? 0;
        var stopped = false;
        foreach (var itemId in pending)
        {
            await gate.VerifyAsync(ct);
            await using var scope = scopes.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<SourceItemProcessingService>();
            var result = await processor.ProcessAsync(itemId, activePrompt, aiPausedUntil,
                gate.VerifyAsync, PauseForBudgetAsync, ct);
            aiPausedUntil = result.AiPausedUntil;
            count++;
            lastId = itemId;
            if (result.StopAi) { stopped = true; break; }
        }
        return (count, lastId, stopped);
    }

    /// <summary>يوقف AI حتى بداية اليوم الفلسطيني التالي عند بلوغ الميزانية.</summary>
    private async Task<DateTimeOffset> PauseForBudgetAsync(DateTimeOffset now, CancellationToken ct)
    {
        var until = AiGovernor.NextPalestineDay(now);
        await AiGovernor.PauseAsync(
            db,
            until,
            $"Daily AI token budget ({AiGovernor.DailyTokenBudget(config)}) reached.",
            ct
        );
        log.LogWarning(
            "Daily AI token budget reached; AI paused until {Until}.",
            SourceSchedule.Display(until)
        );
        return until;
    }

}
