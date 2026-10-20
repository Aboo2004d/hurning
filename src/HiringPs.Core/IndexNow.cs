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

/// <summary>
/// Canonical public URLs, matching the Web's SiteSeo/SiteRoutes form (Seo:PublicOrigin + /ar|/en + route).
/// Details URLs are slug-less; the Web may redirect to a slugged form.
/// </summary>
public static class PublicUrls
{
    public static string Origin(IConfiguration configuration)
    {
        var value = configuration["Seo:PublicOrigin"] ?? "https://hiring.ps";
        if (
            !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != "https"
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.IsDefaultPort
            || uri.AbsolutePath != "/"
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
        )
            throw new InvalidOperationException("PublicOriginRequired");
        return uri.GetLeftPart(UriPartial.Authority);
    }

    public static string Details(string origin, long id, string language) =>
        origin
        + (language == "en" ? "/en" : "/ar")
        + "/Opportunities/Details/"
        + id.ToString(CultureInfo.InvariantCulture);

    public static string Listing(string origin, string language, OpportunityKind kind) =>
        origin + (language == "en" ? "/en" : "/ar") + "/?kind=" + kind;

    public static string WithSource(string url, string source) =>
        url + (url.Contains('?') ? "&" : "?") + "utm_source=" + Uri.EscapeDataString(source);
}

// One row per opportunity: both language URLs are submitted together and share the record.
public sealed class IndexNowEntry
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long OpportunityId { get; set; }
    public Opportunity Opportunity { get; set; } = null!;
    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;

    // Pending while ProcessedAt is null or older than QueuedAt.
    public DateTimeOffset? ProcessedAt { get; set; }

    // Last accepted submission of the ar/en URLs to IndexNow.
    public DateTimeOffset? SubmittedAt { get; set; }

    // Status seen when last processed; detects bulk changes that bypass SaveChanges.
    public PublicationStatus? SubmittedStatus { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public int? LastStatus { get; set; }

    [MaxLength(200)]
    public string LastError { get; set; } = "";
}

public static class IndexNowSettings
{
    public const string DefaultEndpoint = "https://api.indexnow.org/indexnow";
    public const int MaxUrls = 10000;

    public static bool IsKeyFormat(string? key) =>
        key is { Length: >= 8 and <= 128 } && Regex.IsMatch(key, "^[A-Za-z0-9-]+$", RegexOptions.CultureInvariant);

    /// <summary>The configured key, or null when missing or invalid. Served at /{key}.txt.</summary>
    public static string? Key(IConfiguration configuration)
    {
        var key = configuration["IndexNow:Key"]?.Trim();
        return IsKeyFormat(key) ? key : null;
    }

    public static bool Enabled(IConfiguration configuration) =>
        configuration.GetValue<bool>("IndexNow:Enabled") && Key(configuration) is not null;
}

/// <summary>Called from HiringDb.SaveChanges to queue public-page changes. No network work here.</summary>
public static class IndexNowQueue
{
    private static readonly string[] SignificantFields =
    [
        nameof(Opportunity.Kind),
        nameof(Opportunity.Title),
        nameof(Opportunity.Organization),
        nameof(Opportunity.Summary),
        nameof(Opportunity.Locations),
        nameof(Opportunity.Deadline),
        nameof(Opportunity.ApplyUrl),
        nameof(Opportunity.ClosureEvidence),
    ];

    public static bool Affects(EntityEntry<Opportunity> entry)
    {
        if (entry.State == EntityState.Added)
            return entry.Entity.Status == PublicationStatus.Published;
        if (entry.State != EntityState.Modified)
            return false;
        var original = entry.Property(x => x.Status).OriginalValue;
        var current = entry.Entity.Status;
        if (original != current)
            return original == PublicationStatus.Published || current == PublicationStatus.Published;
        return current == PublicationStatus.Published
            && SignificantFields.Any(name =>
            {
                var property = entry.Property(name);
                return property.IsModified && !Equals(property.OriginalValue, property.CurrentValue);
            });
    }

    public static void Touch(HiringDb db, Opportunity item)
    {
        var now = DateTimeOffset.UtcNow;
        var existing =
            db.IndexNowEntries.Local.FirstOrDefault(x =>
                x.Opportunity == item || (item.Id != 0 && x.OpportunityId == item.Id)
            ) ?? (item.Id != 0 ? db.IndexNowEntries.Find(item.Id) : null);
        if (existing is not null)
            existing.QueuedAt = now;
        else
            db.IndexNowEntries.Add(new IndexNowEntry { Opportunity = item, QueuedAt = now });
    }
}

public sealed class IndexNowSubmitter(
    HiringDb db,
    IHttpClientFactory clients,
    IConfiguration configuration,
    ILogger<IndexNowSubmitter>? logger = null
)
{
    public const string HttpClientName = "IndexNow";

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

    private string Endpoint()
    {
        var value = configuration["IndexNow:Endpoint"];
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https"
            ? uri.ToString()
            : IndexNowSettings.DefaultEndpoint;
    }
}
