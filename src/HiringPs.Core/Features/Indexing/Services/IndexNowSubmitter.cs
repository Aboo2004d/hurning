// إبلاغ محركات البحث عبر IndexNow بتغير صفحات الفرص العامة، على دفعات من العامل وليس داخل طلبات الويب.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>مكون IndexNowSubmitter ضمن مجال Indexing؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public sealed class IndexNowSubmitter(
    HiringDb db,
    IHttpClientFactory clients,
    IConfiguration configuration,
    ILogger<IndexNowSubmitter>? logger = null
)
{
    public const string HttpClientName = "IndexNow";

    /// <summary>ينفذ عمل الدورة أو الإخطار وفق الإعدادات والطابور دون تغيير عقود المزود.</summary>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        if (!IndexNowSettings.Enabled(configuration))
            return 0;
        var key = IndexNowSettings.Key(configuration)!;
        string origin;
        try
        {
            origin = PublicUrls.Origin(configuration);
        }
        catch (InvalidOperationException)
        {
            logger?.LogWarning("IndexNow skipped: Seo:PublicOrigin is not a valid HTTPS origin.");
            return 0;
        }
        var now = DateTimeOffset.UtcNow;
        // Bulk status updates (e.g. deadline archiving) bypass SaveChanges; re-queue those pages here.
        await db
            .IndexNowEntries.Where(x =>
                x.ProcessedAt != null
                && x.ProcessedAt >= x.QueuedAt
                && x.SubmittedStatus != x.Opportunity.Status
            )
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.QueuedAt, now), ct);
        var batch = await db
            .IndexNowEntries.Include(x => x.Opportunity)
            .Where(x =>
                (x.ProcessedAt == null || x.ProcessedAt < x.QueuedAt)
                && (x.NextAttemptAt == null || x.NextAttemptAt <= now)
            )
            .OrderBy(x => x.QueuedAt)
            .Take(IndexNowSettings.MaxUrls / 2)
            .ToListAsync(ct);
        if (batch.Count == 0)
            return 0;
        var publicKinds = await db
            .OpportunityAccessRules.AsNoTracking()
            .Where(x => x.PublicDetails)
            .Select(x => x.Kind)
            .ToListAsync(ct);
        var submit = new List<IndexNowEntry>();
        var urls = new List<string>();
        foreach (var entry in batch)
        {
            var item = entry.Opportunity;
            // Gated details pages are not in the sitemap either; a page that was never public is skipped.
            var include =
                publicKinds.Contains(item.Kind)
                && (item.Status is PublicationStatus.Published or PublicationStatus.Archived || entry.SubmittedAt != null);
            if (!include)
            {
                entry.ProcessedAt = now;
                entry.SubmittedStatus = item.Status;
                entry.LastStatus = null;
                entry.LastError = "NotPublic";
                continue;
            }
            submit.Add(entry);
            urls.Add(PublicUrls.Details(origin, item.Id, "ar"));
            urls.Add(PublicUrls.Details(origin, item.Id, "en"));
        }
        if (submit.Count > 0)
        {
            int? status = null;
            var error = "";
            try
            {
                using var response = await clients
                    .CreateClient(HttpClientName)
                    .PostAsJsonAsync(
                        Endpoint(),
                        new
                        {
                            host = new Uri(origin).Host,
                            key,
                            keyLocation = origin + "/" + key + ".txt",
                            urlList = urls,
                        },
                        ct
                    );
                status = (int)response.StatusCode;
                if (!response.IsSuccessStatusCode)
                    error = status switch
                    {
                        400 => "IndexNowBadRequest",
                        403 => "IndexNowKeyInvalid",
                        422 => "IndexNowUrlsRejected",
                        429 => "IndexNowRateLimited",
                        _ => "IndexNowUnavailable",
                    };
            }
            catch (HttpRequestException)
            {
                error = "IndexNowUnavailable";
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                error = "IndexNowUnavailable";
            }
            foreach (var entry in submit)
            {
                entry.LastStatus = status;
                entry.LastError = error;
                if (error.Length == 0)
                {
                    entry.ProcessedAt = now;
                    entry.SubmittedAt = now;
                    entry.SubmittedStatus = entry.Opportunity.Status;
                    entry.Attempts = 0;
                    entry.NextAttemptAt = null;
                }
                else
                {
                    entry.Attempts++;
                    // 5, 10, 20 ... minutes, capped at one day; rate limits wait at least an hour.
                    var minutes = Math.Min(1440, 5 * Math.Pow(2, Math.Min(10, entry.Attempts - 1)));
                    if (status == 429)
                        minutes = Math.Max(60, minutes);
                    entry.NextAttemptAt = now.AddMinutes(minutes);
                }
            }
            logger?.LogInformation(
                "IndexNow submitted {Count} URLs HttpStatus {HttpStatus} Code {Code}",
                urls.Count,
                status,
                error
            );
        }
        await db.SaveChangesAsync(CancellationToken.None);
        return urls.Count;
    }

    /// <summary>ينفذ Endpoint وفق قواعد IndexNowSubmitter الحالية دون تغيير عقد المستدعين.</summary>
    private string Endpoint()
    {
        var value = configuration["IndexNow:Endpoint"];
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https"
            ? uri.ToString()
            : IndexNowSettings.DefaultEndpoint;
    }
}
