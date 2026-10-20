using System.Text.RegularExpressions;
using System.Xml.Linq;
using AngleSharp.Html.Parser;

namespace HiringPs.Core;

/// <summary>مكون OpportunityRss ضمن مجال Opportunities؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class OpportunityRss
{
    public const string JobsUrl = "https://www.jobs.ps/rss/jobs/gaza-jobs";
    public const string ReliefUrl = "https://reliefweb.int/jobs/rss.xml";

    /// <summary>ينفذ Endpoint وفق قواعد OpportunityRss الحالية دون تغيير عقد المستدعين.</summary>
    public static string Endpoint(ConnectorKind kind) =>
        kind == ConnectorKind.JobsRss ? JobsUrl : ReliefUrl;

    // Parsed feeds are reused for a short window: discovery fills it and each item's detail
    // read in the same run takes its entry from memory. Failures are never cached.
    public static readonly TimeSpan CacheAge = TimeSpan.FromMinutes(10);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<
        string,
        (DateTimeOffset At, Dictionary<string, ContentBundle> Entries)
    > Cache = new();

    /// <summary>ينفذ ReadCachedAsync وفق قواعد OpportunityRss الحالية دون تغيير عقد المستدعين.</summary>
    public static async Task<Dictionary<string, ContentBundle>> ReadCachedAsync(
        Source source,
        UrlPolicy policy,
        CancellationToken ct
    )
    {
        if (
            Cache.TryGetValue(CacheKey(source), out var hit)
            && hit.At > DateTimeOffset.UtcNow - CacheAge
        )
            return hit.Entries;
        return await ReadAsync(source, policy, ct);
    }

    /// <summary>ينفذ CacheKey وفق قواعد OpportunityRss الحالية دون تغيير عقد المستدعين.</summary>
    private static string CacheKey(Source source) => $"{source.Connector}|{source.Url}";

    /// <summary>ينفذ ReadAsync وفق قواعد OpportunityRss الحالية دون تغيير عقد المستدعين.</summary>
    public static async Task<Dictionary<string, ContentBundle>> ReadAsync(
        Source source,
        UrlPolicy policy,
        CancellationToken ct
    )
    {
        var entries = await ReadFeedAsync(source, policy, ct);
        Cache[CacheKey(source)] = (DateTimeOffset.UtcNow, entries);
        return entries;
    }

    /// <summary>ينفذ ReadFeedAsync وفق قواعد OpportunityRss الحالية دون تغيير عقد المستدعين.</summary>
    private static async Task<Dictionary<string, ContentBundle>> ReadFeedAsync(
        Source source,
        UrlPolicy policy,
        CancellationToken ct
    )
    {
        var endpoint = Endpoint(source.Connector);
        if (source.Url != endpoint)
            throw new InvalidOperationException(
                "RSS connector requires its official tested feed URL."
            );
        var feed = await UnCareersFeed.ReadXmlAsync(
            endpoint,
            [new Uri(endpoint).IdnHost],
            policy,
            ct
        );
        return Parse(feed, source.Connector);
    }

    /// <summary>ينفذ Parse وفق قواعد OpportunityRss الحالية دون تغيير عقد المستدعين.</summary>
    public static Dictionary<string, ContentBundle> Parse(XDocument feed, ConnectorKind kind)
    {
        if (feed.Root?.Name != "rss" || feed.Root.Element("channel") is not { } channel)
            throw new InvalidOperationException("Expected RSS, not a protection page.");
        var result = new Dictionary<string, ContentBundle>();
        foreach (var item in channel.Elements("item"))
        {
            if (
                !Uri.TryCreate(item.Element("link")?.Value, UriKind.Absolute, out var link)
                || link.Scheme != "https"
                || link.UserInfo.Length > 0
                || !link.IsDefaultPort
                || link.IdnHost != new Uri(Endpoint(kind)).IdnHost
            )
                throw new InvalidOperationException("Unexpected RSS job link.");
            var doc = new HtmlParser().ParseDocument(item.Element("description")?.Value ?? "");
            foreach (var e in doc.QuerySelectorAll("script,style,iframe,nav,footer"))
                e.Remove();
            if (kind == ConnectorKind.ReliefWebRss)
            {
                var countries = doc.QuerySelectorAll(".country")
                    .Select(e => e.TextContent)
                    .ToArray();
                if (
                    !countries.Any(c =>
                        c.Contains(
                            "occupied Palestinian territory",
                            StringComparison.OrdinalIgnoreCase
                        ) || c.Contains("State of Palestine", StringComparison.OrdinalIgnoreCase)
                    )
                )
                    continue;
            }

            var text = Regex.Replace(doc.Body?.TextContent ?? "", @"\s+", " ").Trim();
            if (text.Length < 80)
                throw new ContentNeedsReviewException(
                    "RSS description incomplete; source needs review."
                );
            var links = doc.QuerySelectorAll("a[href]")
                .Select(a => Uri.TryCreate(link, a.GetAttribute("href"), out var uri) ? uri : null)
                .Where(u => u is not null && u.Scheme == "https")
                .Select(u => u!.ToString());
            result[link.ToString()] = new(
                (item.Element("title")?.Value ?? "")
                    + "\n"
                    + text
                    + "\nSOURCE LINKS:\n"
                    + link
                    + "\n"
                    + string.Join('\n', links.Take(30)),
                []
            );
        }

        return result;
    }
}
