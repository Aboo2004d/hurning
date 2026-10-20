// استخراج البيانات بالنموذج، ثم التحقق من الأدلة وتحديد النشر أو المراجعة أو الأرشفة.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>ينسق اكتشاف المصادر والاستخراج والمراجعة وطلبات الجمع اليدوي مع حجز SQL.</summary>
public class CollectionPipeline(
    HiringDb db,
    BrowserCollector browser,
    OpportunityService opportunities,
    IConfiguration config,
    ILogger<CollectionPipeline> log,
    IServiceScopeFactory scopes
)
{
    /// <summary>ينفذ دورة طابور الجمع ويعزل تقدم كل مصدر وطلب يدوي.</summary>
    public async Task TickAsync(CancellationToken ct)
    {
        await using var gate = new SqlRunLock(config.GetConnectionString("Hiring")!);
        if (!await gate.AcquireAsync(ct))
            return;
        var activePrompt = await PromptWorkflow.ActivateAsync(db, ct);
        await RetentionCleanup.RunAsync(db, config, ct);
        var now = DateTimeOffset.UtcNow;
        var heartbeat = await db.Heartbeats.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (heartbeat is null)
        {
            heartbeat = new();
            db.Heartbeats.Add(heartbeat);
        }

        heartbeat.At = now;
        heartbeat.State = "Running";
        await db.SaveChangesAsync(ct);
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
        {
            await gate.VerifyAsync(ct);
            var run = new ScrapeRun { SourceId = source.Id };
            db.Runs.Add(run);
            await db.SaveChangesAsync(ct);
            try
            {
                var configurationHash = TextRules.Hash(
                    $"{source.Connector}|{source.Url}|{source.ConfigurationJson}"
                );
                var checkpoint = await db.DiscoveryCheckpoints.SingleOrDefaultAsync(
                    x => x.SourceId == source.Id,
                    ct
                );
                if (checkpoint is null)
                {
                    checkpoint = new()
                    {
                        SourceId = source.Id,
                        ConfigurationHash = configurationHash,
                        NextUrl = source.Url,
                    };
                    db.DiscoveryCheckpoints.Add(checkpoint);
                    await db.SaveChangesAsync(ct);
                }
                else if (checkpoint.ConfigurationHash != configurationHash)
                {
                    // Configuration changes explicitly start a fresh cursor; saved items remain intact.
                    checkpoint.ConfigurationHash = configurationHash;
                    checkpoint.NextUrl = source.Url;
                    checkpoint.CompletedPages = checkpoint.ReplayClicks = checkpoint.Found = 0;
                    checkpoint.Completed = false;
                    checkpoint.VisitedUrlsJson = "[]";
                    await db.SaveChangesAsync(ct);
                }
                run.Found = checkpoint.Found;
                await browser.DiscoverResumableAsync(
                    source,
                    checkpoint,
                    async (batch, token) =>
                    {
                        await gate.VerifyAsync(token);
                        // Keep failed batch tracking isolated: a later error save must never commit a partial page.
                        await using var pageScope = scopes.CreateAsyncScope();
                        var store = pageScope.ServiceProvider.GetRequiredService<DiscoveryBatchStore>();
                        await store.SaveAsync(source.Id, source.Connector, run.Id, activePrompt.Id, batch, token);
                        await db.Entry(checkpoint).ReloadAsync(token);
                        await db.Entry(run).ReloadAsync(token);
                    },
                    ct
                );
                db.DiscoveryCheckpoints.Remove(checkpoint);
                await db.Entry(source).ReloadAsync(ct);
                var finished = DateTimeOffset.UtcNow;
                source.LastSuccessAt = finished;
                source.LastError = "";
                source.CollectionRetryAt = null;
                source.ScheduleAnchorDate ??= SourceSchedule.LocalDate(finished);
                if (manual is null || source.NextDueAt <= finished)
                    source.NextDueAt = SourceSchedule.NextAfter(source, finished);
                if (manual is not null)
                    manual.DiscoveryFinished = true;
                run.Status = "Success";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (ex is CollectorDiagnosticException detail)
                    db.Audit.Add(
                        new()
                        {
                            Actor = "Collector",
                            Action = "Collector.Diagnostic",
                            Detail = CollectorDiagnosticFiles.AuditDetail(detail.Diagnostic),
                        }
                    );
                await db.Entry(source).ReloadAsync(ct);
                source.LastError = CollectionErrorRules.SafeError(ex);
                source.CollectionRetryAt = DateTimeOffset.UtcNow.AddMinutes(30);
                run.Status = "Failed";
                run.Error = source.LastError;
                if (manual is not null)
                {
                    manual.Status = "Failed";
                    manual.Error = source.LastError.Truncate(1000);
                    manual.FinishedAt = DateTimeOffset.UtcNow;
                }

                log.LogWarning(
                    "Source {Id} collection failed: {Type}",
                    source.Id,
                    ex.GetType().Name
                );
            }

            run.FinishedAt = DateTimeOffset.UtcNow;
            await gate.VerifyAsync(ct);
            await db.SaveChangesAsync(ct);
        }

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
        if (manual is not null)
            pendingQuery = pendingQuery.Where(x => x.SourceId == manual.SourceId);
        var pending = await pendingQuery
            .OrderBy(x => x.FirstSeenAt)
            .Select(x => x.Id)
            .Take(15)
            .ToListAsync(ct);
        foreach (var itemId in pending)
        {
            await gate.VerifyAsync(ct);
            await using var scope = scopes.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<SourceItemProcessingService>();
            await processor.ProcessAsync(itemId, activePrompt, gate.VerifyAsync, ct);
        }

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
        heartbeat.At = DateTimeOffset.UtcNow;
        heartbeat.State = "Idle";
        await db.SaveChangesAsync(ct);
    }

}
