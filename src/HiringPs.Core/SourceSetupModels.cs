using System.Net;
using System.Text.Json;
using AngleSharp.Html.Parser;

namespace HiringPs.Core;

public sealed record SourceSetupLink(string Url, string Text);

public sealed record SourceSetupSnapshot(
    string Url,
    string Title,
    string Text,
    string Html,
    SourceSetupLink[] Links
);

public sealed record SourceSetupPreview(string[] Links, string? Next, string SampleText);

public sealed class SourceSetupSuggestion
{
    public bool Supported { get; set; }
    public string Name { get; set; } = "";
    public string ListingUrl { get; set; } = "";
    public string Connector { get; set; } = "";
    public string ListingLinkSelector { get; set; } = "";
    public string ContentSelector { get; set; } = "body";
    public string NextSelector { get; set; } = "";
    public string Explanation { get; set; } = "";
}

public sealed record SourceSetupResult(
    string Name,
    string Url,
    int Connector,
    string ConfigurationJson,
    string Explanation,
    SourceSetupPreview Preview,
    bool Verified = true
);

public static class SourceSetupRules
{
    public static Uri Website(string value)
    {
        if (
            !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != "https"
            || uri.UserInfo.Length > 0
            || !uri.IsDefaultPort
            || value.Length > 1000
        )
            throw new InvalidOperationException("SourceSetupInvalidUrl");
        if (
            uri.IsLoopback
            || (
                IPAddress.TryParse(uri.IdnHost.Trim('[', ']'), out var address)
                && UrlPolicy.IsPrivate(address)
            )
        )
            throw new InvalidOperationException("SourceSetupInvalidUrl");
        var platforms = new[]
        {
            "linkedin.com",
            "facebook.com",
            "fb.com",
            "fb.watch",
            "t.me",
            "telegram.me",
            "telegram.org",
        };
        if (
            platforms.Any(host =>
                uri.IdnHost == host
                || uri.IdnHost.EndsWith("." + host, StringComparison.OrdinalIgnoreCase)
            )
        )
            throw new InvalidOperationException("SourceSetupSocialDeferred");
        return uri;
    }

    public static bool IsObserved(string url, SourceSetupSnapshot observed)
    {
        var listing = Website(url);
        var origin = Website(observed.Url);
        return listing.IdnHost == origin.IdnHost
            && (
                listing == origin
                || observed.Links.Any(x =>
                    Uri.TryCreate(x.Url, UriKind.Absolute, out var uri) && uri == listing
                )
            );
    }

    public static Source Candidate(
        SourceSetupSuggestion suggestion,
        SourceSetupSnapshot observed,
        bool allowManualProposal = false
    )
    {
        if (!suggestion.Supported)
            throw new InvalidOperationException("SourceSetupUnsupported");
        var listing = Website(suggestion.ListingUrl);
        if (!allowManualProposal && !IsObserved(suggestion.ListingUrl, observed))
            throw new InvalidOperationException("SourceSetupUnseenUrl");
        var connector = suggestion.Connector switch
        {
            "Automatic" => ConnectorKind.Automatic,
            "BrowserSelectors" => ConnectorKind.BrowserSelectors,
            "JobsRss" when listing.AbsoluteUri == OpportunityRss.JobsUrl => ConnectorKind.JobsRss,
            "ReliefWebRss" when listing.AbsoluteUri == OpportunityRss.ReliefUrl =>
                ConnectorKind.ReliefWebRss,
            _ => throw new InvalidOperationException("SourceSetupUnsupported"),
        };
        if (
            suggestion.Name.Length > 160
            || suggestion.Explanation.Length > 2000
            || new[]
            {
                suggestion.ListingLinkSelector,
                suggestion.ContentSelector,
                suggestion.NextSelector,
            }.Any(x => x.Length > 500)
            || connector == ConnectorKind.BrowserSelectors
                && string.IsNullOrWhiteSpace(suggestion.ListingLinkSelector)
        )
            throw new InvalidOperationException("SourceSetupInvalidReply");
        return new Source
        {
            Name = suggestion.Name,
            Url = listing.AbsoluteUri,
            Connector = connector,
            // In-memory preview only; never saved or enabled by this assistant.
            Enabled = true,
            UsageApproved = true,
            ConfigurationJson = JsonSerializer.Serialize(
                new SourceConfiguration
                {
                    AllowedHosts = [listing.IdnHost],
                    ListingLinkSelector = suggestion.ListingLinkSelector,
                    ContentSelector = string.IsNullOrWhiteSpace(suggestion.ContentSelector)
                        ? "body"
                        : suggestion.ContentSelector,
                    NextSelector = suggestion.NextSelector,
                    MaxPages = 10,
                }
            ),
        };
    }

    public static SourceSetupSnapshot Snapshot(string html, string url)
    {
        if (html.Length > 2_000_000)
            throw new InvalidOperationException("SourceSetupPageTooLarge");
        var origin = Website(url);
        var doc = new HtmlParser().ParseDocument(html);
        foreach (
            var node in doc.QuerySelectorAll("script,style,noscript,iframe,input,textarea,svg")
        )
            node.Remove();
        var links = doc.QuerySelectorAll("a[href],link[rel='alternate'][href]")
            .Select(a =>
                (
                    Element: a,
                    Uri: Uri.TryCreate(origin, a.GetAttribute("href"), out var uri) ? uri : null
                )
            )
            .Where(x =>
                x.Uri is not null
                && x.Uri.Scheme == "https"
                && x.Uri.IdnHost == origin.IdnHost
                && x.Uri.UserInfo.Length == 0
                && x.Uri.IsDefaultPort
                && x.Uri.AbsoluteUri.Length <= 1000
            )
            .Select(x => new SourceSetupLink(
                new UriBuilder(x.Uri!) { Fragment = "" }
                    .Uri
                    .AbsoluteUri,
                x.Element.TextContent.Trim().Truncate(160)
            ))
            .DistinctBy(x => x.Url)
            .Take(200)
            .ToArray();
        var fragments = doc.QuerySelectorAll("a[href],link[rel='alternate'][href]")
            .Take(100)
            .Select(x => (x.ParentElement?.OuterHtml ?? x.OuterHtml).Truncate(800));
        return new(
            url,
            (doc.Title ?? "").Truncate(200),
            (doc.Body?.TextContent ?? "").Truncate(12000),
            string.Join('\n', fragments).Truncate(40000),
            links
        );
    }
}
