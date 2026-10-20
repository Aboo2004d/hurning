// استخراج البيانات بالنموذج، ثم التحقق من الأدلة وتحديد النشر أو المراجعة أو الأرشفة.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>ينفذ اكتشاف مصدر مع نقاط الاستئناف وصحة المصدر وجدولته، ضمن قفل الدورة الحالي.</summary>
public sealed class SourceDiscoveryService(HiringDb db, BrowserCollector browser, IConfiguration config,
    ILogger<CollectionPipeline> log, IServiceScopeFactory scopes)
{
    /// <summary>يكتشف المصدر ويثبت الدفعات ويحدث الصحة والجدولة تحت قفل الدورة.</summary>
    public async Task DiscoverAsync(
        SqlRunLock gate,
        Source source,
        SourceCollectionRequest? manual,
        PromptVersion activePrompt,
        CancellationToken ct
    )
    {
        await gate.VerifyAsync(ct);
        var run = new ScrapeRun { SourceId = source.Id };
        db.Runs.Add(run);
        await db.SaveChangesAsync(ct);
        var succeeded = false;
        var reverifyBefore = DateTimeOffset.UtcNow - SourceHealth.ReverifyInterval(config);
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
                    await store.SaveAsync(source.Id, source.Connector, run.Id, activePrompt.Id, reverifyBefore, batch, token);
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
            await SourceHealth.RecordSuccessAsync(db, source, run, config, ct);
            source.ScheduleAnchorDate ??= SourceSchedule.LocalDate(finished);
            if (manual is null || source.NextDueAt <= finished)
                source.NextDueAt = SourceSchedule.NextAfter(source, finished);
            if (manual is not null)
                manual.DiscoveryFinished = true;
            run.Status = "Success";
            succeeded = true;
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
            // Exponential backoff (30 min doubling, 24 h cap); flagged for review after N failures.
            SourceHealth.RecordFailure(source, config, DateTimeOffset.UtcNow);
            run.Status = "Failed";
            run.Error = source.LastError;
            if (manual is not null)
            {
                manual.Status = "Failed";
                manual.Error = source.LastError.Truncate(1000);
                manual.FinishedAt = DateTimeOffset.UtcNow;
            }

            log.LogWarning(
                "Source {Id} collection failed ({Failures} in a row): {Type}",
                source.Id,
                source.ConsecutiveFailures,
                ex.GetType().Name
            );
        }

        run.FinishedAt = DateTimeOffset.UtcNow;
        await gate.VerifyAsync(ct);
        await db.SaveChangesAsync(ct);
        if (!succeeded)
            return;
        try
        {
            await using var healthScope = scopes.CreateAsyncScope();
            var flagged = await SourceHealth.FlagUnseenAsync(
                healthScope.ServiceProvider.GetRequiredService<HiringDb>(),
                source,
                config,
                ct
            );
            if (flagged > 0)
                log.LogInformation(
                    "Source {Id}: {Count} published records not seen in the listing moved to review.",
                    source.Id,
                    flagged
                );
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogWarning("Source {Id} listing check failed: {Type}", source.Id, ex.GetType().Name);
        }
    }

}
