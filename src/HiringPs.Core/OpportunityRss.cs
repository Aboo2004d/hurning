using System.Text.RegularExpressions;
using System.Xml.Linq;
using AngleSharp.Html.Parser;

namespace HiringPs.Core;

public static class OpportunityRss
{
    public const string JobsUrl = "https://www.jobs.ps/rss/jobs/gaza-jobs";
    public const string ReliefUrl = "https://reliefweb.int/jobs/rss.xml";

    public static string Endpoint(ConnectorKind kind) =>
        kind == ConnectorKind.JobsRss ? JobsUrl : ReliefUrl;

    public static async Task<Dictionary<string, ContentBundle>> ReadAsync(
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
