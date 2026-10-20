// صحة المصادر: تباعد إعادة المحاولة بعد الفشل، كشف تغيّر القالب، والإعلانات التي اختفت من القائمة.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

/// <summary>مكون SourceHealth ضمن مجال Collection؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class SourceHealth
{
    public const string FailureWarning = "Repeated collection failures";
    public const string TemplateWarning = "Possible template change";

    // 30 minutes, doubling per consecutive failure, capped at 24 hours.
    /// <summary>30 minutes, doubling per consecutive failure, capped at 24 hours.</summary>
    public static TimeSpan FailureBackoff(int failures) =>
        TimeSpan.FromMinutes(Math.Min(1440, 30 * Math.Pow(2, Math.Clamp(failures - 1, 0, 10))));

    /// <summary>ينفذ FailureReviewThreshold وفق قواعد SourceHealth الحالية دون تغيير عقد المستدعين.</summary>
    public static int FailureReviewThreshold(IConfiguration config) =>
        Math.Clamp(config.GetValue<int?>("Collector:SourceFailureReviewThreshold") ?? 5, 1, 100);

    /// <summary>ينفذ ReverifyInterval وفق قواعد SourceHealth الحالية دون تغيير عقد المستدعين.</summary>
    public static TimeSpan ReverifyInterval(IConfiguration config) =>
        TimeSpan.FromHours(Math.Clamp(config.GetValue<int?>("Collector:ReverifyHours") ?? 24, 1, 720));

    /// <summary>يرفع عداد فشل المصدر ويحدد مهلة إعادة الجمع والتحذير.</summary>
    public static void RecordFailure(Source source, IConfiguration config, DateTimeOffset now)
    {
        source.ConsecutiveFailures++;
        source.CollectionRetryAt = now + FailureBackoff(source.ConsecutiveFailures);
        if (source.ConsecutiveFailures >= FailureReviewThreshold(config))
            SetWarning(
                source,
                $"{FailureWarning}: {source.ConsecutiveFailures} in a row; review the source configuration.",
                now
            );
    }

    // Pure rule: a sudden drop against the recent successful average suggests a changed layout.
    /// <summary>Pure rule: a sudden drop against the recent successful average suggests a changed layout.</summary>
    public static string? TemplateChange(int found, IReadOnlyCollection<int> previous, double ratio = 0.3)
    {
        if (previous.Count < 3)
            return null;
        var average = previous.Average();
        if (average < 1)
            return null;
        return found == 0 || found < average * ratio
            ? $"{TemplateWarning}: found {found} listings against a recent average of {average:0.#}; check selectors or layout."
            : null;
    }

    /// <summary>يحدث صحة المصدر بعد نجاح اكتشافه ويكشف تغير عدد النتائج.</summary>
    public static async Task RecordSuccessAsync(
        HiringDb db,
        Source source,
        ScrapeRun run,
        IConfiguration config,
        CancellationToken ct
    )
    {
        source.ConsecutiveFailures = 0;
        var window = Math.Clamp(config.GetValue<int?>("Collector:HealthWindow") ?? 5, 3, 50);
        var previous = await db
            .Runs.AsNoTracking()
            .Where(x => x.SourceId == source.Id && x.Status == "Success" && x.Id != run.Id)
            .OrderByDescending(x => x.StartedAt)
            .Take(window)
            .Select(x => x.Found)
            .ToListAsync(ct);
        var warning = TemplateChange(run.Found, previous);
        if (warning is not null)
            SetWarning(source, warning, DateTimeOffset.UtcNow);
        else if (source.HealthWarning.Length > 0)
        {
            source.HealthWarning = "";
            source.HealthWarningAt = null;
        }
    }

    /// <summary>ينفذ SetWarning وفق قواعد SourceHealth الحالية دون تغيير عقد المستدعين.</summary>
    private static void SetWarning(Source source, string warning, DateTimeOffset now)
    {
        if (source.HealthWarning != warning)
            source.HealthWarningAt = now;
        source.HealthWarning = warning.Truncate(500);
    }

    // Rolling feeds drop entries by design; absence there is not evidence of closure.
    /// <summary>Rolling feeds drop entries by design; absence there is not evidence of closure.</summary>
    public static bool TracksListing(ConnectorKind connector) =>
        connector
            is not (
                ConnectorKind.JobsRss
                or ConnectorKind.ReliefWebRss
                or ConnectorKind.UnrwaInspira
            );

    // Published records absent from the listing for N complete discovery passes go to review.
    // They are never archived automatically.
    /// <summary>ينقل المنشورات غير المشاهدة للمراجعة وفق سياسة المصدر دون إعلان إغلاقها.</summary>
    public static async Task<int> FlagUnseenAsync(
        HiringDb db,
        Source source,
        IConfiguration config,
        CancellationToken ct
    )
    {
        var runs = Math.Clamp(config.GetValue<int?>("Collector:UnseenRunsBeforeReview") ?? 3, 0, 50);
        if (runs == 0 || !TracksListing(source.Connector))
            return 0;
        // Each pass ends at a successful run; the (N+1)th finish marks the start of the last N passes.
        var finishes = await db
            .Runs.AsNoTracking()
            .Where(x => x.SourceId == source.Id && x.Status == "Success" && x.FinishedAt != null)
            .OrderByDescending(x => x.FinishedAt)
            .Take(runs + 1)
            .Select(x => x.FinishedAt)
            .ToListAsync(ct);
        if (finishes.Count <= runs || finishes[^1] is not { } cutoff)
            return 0;
        // Records past their deadline are archived by the deadline rule instead.
        var today = TextRules.PalestineToday();
        var stale = await db
            .Opportunities.Where(o =>
                o.Status == PublicationStatus.Published
                && !o.AdminOverride
                && (o.Deadline == null || o.Deadline >= today)
                && o.SourceLinks.Any(l => l.SourceItem.SourceId == source.Id)
                && !o.SourceLinks.Any(l => l.SourceItem.LastSeenAt >= cutoff)
            )
            .OrderBy(o => o.Id)
            .Take(200)
            .ToListAsync(ct);
        foreach (var opportunity in stale)
        {
            opportunity.Status = PublicationStatus.NeedsReview;
            opportunity.ReviewReason =
                $"Not seen in source listing for {runs} successful discovery runs; verify whether it closed (never archived automatically).";
        }
        if (stale.Count > 0)
        {
            db.Audit.Add(
                new()
                {
                    Actor = "Collector",
                    Action = "Opportunity.NotSeenInListing",
                    Detail =
                        $"Source {source.Id}: {stale.Count} published records moved to review: {string.Join(',', stale.Select(x => x.Id))}".Truncate(2000),
                }
            );
            await db.SaveChangesAsync(ct);
        }
        return stale.Count;
    }
}
