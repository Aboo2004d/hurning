// اكتشاف روابط الفرص وجلب صفحاتها ومرفقاتها، مع فحص النطاقات وتسجيل التشخيص.
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

public class SourceConfiguration
{
    public string[] AllowedHosts { get; set; } = [];
    public string ListingLinkSelector { get; set; } = "";
    public string ContentSelector { get; set; } = "body";
    public string WaitSelector { get; set; } = "";
    public string NextSelector { get; set; } = "";
    public int MaxPages { get; set; } = 10;
    public string AppName { get; set; } = "";
    public int ForbiddenWaitSeconds { get; set; } = 20;
}

public class UrlPolicy
{
    public async Task<Uri> ValidateAsync(
        string url,
        IEnumerable<string> hosts,
        CancellationToken ct
    )
    {
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != "https"
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.IsDefaultPort
        )
            throw new InvalidOperationException(
                "Only public HTTPS URLs without credentials are allowed."
            );
        if (!hosts.Any(h => string.Equals(h, uri.IdnHost, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Host is not approved for this source.");
        var addresses = await Dns.GetHostAddressesAsync(uri.IdnHost, ct);
        if (addresses.Length == 0 || addresses.Any(IsPrivate))
            throw new InvalidOperationException(
                "Private/reserved network targets are not allowed."
            );
        return uri;
    }

    public static bool IsPrivate(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
            return true;
        var b = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
            return b[0] is 0 or 10 or 127 or >= 224
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                || (b[0] == 198 && b[1] is 18 or 19);
        return ip.IsIPv6LinkLocal
            || ip.IsIPv6Multicast
            || ip.IsIPv6SiteLocal
            || (b[0] & 0xfe) == 0xfc;
    }
}

// Text used for change detection: main content only, so reordered or rotating link lists
// (SOURCE LINKS, related listings) do not force a new AI extraction.
public static class ContentFingerprint
{
    public const string LinksMarker = "SOURCE LINKS:";

    public static string MainText(string content)
    {
        var links = content.IndexOf(LinksMarker, StringComparison.Ordinal);
        var main = links >= 0 ? content[..links] : content;
        return Regex.Replace(main.Normalize(), @"\s+", " ").Trim();
    }

    public static string Hash(string content) => TextRules.Hash(MainText(content));

    // Hashes stored before this rule covered the complete text; accept either for reuse.
    public static bool Matches(string storedHash, string content) =>
        storedHash.Length > 0
        && (storedHash == Hash(content) || storedHash == TextRules.Hash(content));
}

public partial class BrowserCollector(
    UrlPolicy policy,
    IConfiguration configuration,
    AttachmentReader attachmentReader,
    BrowserHost? host = null
) : IAsyncDisposable
{
    // Without a shared host (web scopes, probes) this collector owns its browser as before.
    private BrowserHost? ownedHost;

    private BrowserHost Host => host ?? (ownedHost ??= new BrowserHost(configuration));

    private async Task<IBrowserContext> ContextAsync(Source source, CancellationToken ct, bool setup = false) =>
        await CollectorDiagnosticFiles.WithoutPageAsync(
            source,
            "Browser",
            "BrowserSetup",
            source.Url,
            configuration,
            () => CreateContextAsync(source, ct, setup),
            ct
        );

    private async Task<IBrowserContext> CreateContextAsync(Source source, CancellationToken ct, bool setup)
    {
        var conf =
            JsonSerializer.Deserialize<SourceConfiguration>(source.ConfigurationJson) ?? new();
        var hosts = conf
            .AllowedHosts.Append(new Uri(source.Url).IdnHost)
            .Concat(
                source.Connector == ConnectorKind.UnrwaInspira ? new[] { "inspira.un.org" } : []
            )
            .ToArray();
        await policy.ValidateAsync(source.Url, hosts, ct);
        var browser = await Host.GetAsync(ct);
        var context = await browser.NewContextAsync(
            new()
            {
                AcceptDownloads = false,
                IgnoreHTTPSErrors = false,
                ServiceWorkers = ServiceWorkerPolicy.Block,
            }
        );
        context.SetDefaultTimeout(20000);
        try
        {
            await RouteAsync(context, hosts, setup, ct);
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
        return context;
    }

    private async Task RouteAsync(
        IBrowserContext context,
        string[] hosts,
        bool setup,
        CancellationToken ct
    )
    {
        await context.RouteAsync(
            "**/*",
            async route =>
            {
                try
                {
                    if (route.Request.ResourceType is "image" or "font" or "media")
                    {
                        await route.AbortAsync();
                        return;
                    }

                    await policy.ValidateAsync(route.Request.Url, hosts, ct);
                    if (setup && route.Request.ResourceType is "script" or "stylesheet")
                    {
                        // Bound parser-blocking dependencies during setup only. Each redirect
                        // returns to the browser and passes the route policy again.
                        try
                        {
                            var response = await route.FetchAsync(new() { Timeout = 10000, MaxRedirects = 0 });
                            try { await route.FulfillAsync(new() { Response = response }); }
                            finally { await response.DisposeAsync(); }
                        }
                        catch (TimeoutException) { await route.AbortAsync("timedout"); }
                        return;
                    }
                    await route.ContinueAsync();
                }
                catch
                {
                    CollectorDiagnosticSession.PolicyBlock(route.Request);
                    await route.AbortAsync();
                }
            }
        );
    }

    private DiscoverySession? discovery;

    private sealed class DiscoverySession(
        DiscoveryCheckpoint checkpoint,
        Func<DiscoveryBatch, CancellationToken, Task> save
    )
    {
        public DiscoveryCheckpoint Checkpoint { get; } = checkpoint;
        public Func<DiscoveryBatch, CancellationToken, Task> Save { get; } = save;
        public bool Saved { get; set; }
    }

    public virtual async Task DiscoverResumableAsync(
        Source source,
        DiscoveryCheckpoint checkpoint,
        Func<DiscoveryBatch, CancellationToken, Task> save,
        CancellationToken ct
    )
    {
        if (checkpoint.Completed)
            return;
        if (discovery is not null)
            throw new InvalidOperationException("Discovery session is already active.");
        discovery = new(checkpoint, save);
        try
        {
            var links = await DiscoverAsync(source, ct);
            if (!discovery.Saved)
                await SaveDiscoveryPageAsync(new(links, null, 1), ct);
        }
        finally
        {
            discovery = null;
        }
    }

    private async Task SaveDiscoveryPageAsync(DiscoveryBatch batch, CancellationToken ct)
    {
        if (discovery is null)
            return;
        await discovery.Save(batch, ct);
        discovery.Saved = true;
    }

    private static async Task AdvanceListingAsync(
        IPage page,
        string selector,
        ILocator next,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        var before = string.Join(
            '|',
            await page.Locator(selector).EvaluateAllAsync<string[]>("els => els.map(e=>e.href)")
        );
        await next.ClickAsync();
        await page.WaitForFunctionAsync(
            "([s,before]) => Array.from(document.querySelectorAll(s)).map(e=>e.href).join('|') !== before",
            new[] { selector, before },
            new() { Timeout = 20000 }
        );
    }

    public virtual async Task<List<(string Id, string Url)>> DiscoverAsync(
        Source source,
        CancellationToken ct
    )
    {
        if (!source.Enabled || !source.UsageApproved)
            throw new InvalidOperationException("Source must be enabled and usage approved.");
        if (source.Connector is ConnectorKind.JobsRss or ConnectorKind.ReliefWebRss)
            return await CollectorDiagnosticFiles.WithoutPageAsync(
                source,
                "Discovery",
                "ReadRss",
                source.Url,
                configuration,
                async () =>
                    (await OpportunityRss.ReadAsync(source, policy, ct))
                        .Keys.Select(url => (TextRules.Hash(url), url))
                        .ToList(),
                ct
            );
        if (source.Connector == ConnectorKind.UnrwaInspira)
        {
            if (source.Url != UnCareersFeed.Url)
                throw new InvalidOperationException(
                    "UNRWA/Inspira requires the official careers.un.org/jobfeed source."
                );
            return await CollectorDiagnosticFiles.WithoutPageAsync(
                source,
                "Discovery",
                "ReadRss",
                source.Url,
                configuration,
                () => UnCareersFeed.DiscoverAsync(policy, ct),
                ct
            );
        }

        if (source.Connector == ConnectorKind.ReliefWeb)
            throw new InvalidOperationException(
                "ReliefWeb requires an approved appname and a tested API adapter; this source remains pending."
            );
        var conf =
            JsonSerializer.Deserialize<SourceConfiguration>(source.ConfigurationJson) ?? new();
        var maxPages = Math.Clamp(conf.MaxPages, 1, 50);
        if (source.Connector == ConnectorKind.Automatic)
        {
            await using var automaticContext = await ContextAsync(source, ct);
            var automaticPage = await automaticContext.NewPageAsync();
            using var diagnostic = new CollectorDiagnosticSession(
                automaticPage,
                source,
                "Discovery",
                source.Url,
                configuration
            );
            return await diagnostic.RunAsync(
                async () =>
                {
                    var visited = discovery is null
                        ? new HashSet<string>()
                        : new HashSet<string>(
                            JsonSerializer.Deserialize<string[]>(
                                discovery.Checkpoint.VisitedUrlsJson
                            ) ?? []
                        );
                    var automaticLinks = new HashSet<string>();
                    string? current = discovery?.Checkpoint.NextUrl is { Length: > 0 } cursor
                        ? cursor
                        : source.Url;
                    for (
                        var n = discovery?.Checkpoint.CompletedPages ?? 0;
                        current is not null && n < maxPages;
                        n++
                    )
                    {
                        ct.ThrowIfCancellationRequested();
                        if (!visited.Add(current))
                            throw new InvalidOperationException(
                                "Automatic pagination loop; source needs review."
                            );
                        await policy.ValidateAsync(current, [new Uri(source.Url).IdnHost], ct);
                        diagnostic.Step("NavigateListing", timeoutMs: 45000);
                        await BrowserNavigation.OpenAsync(
                            automaticPage,
                            current,
                            WaitUntilState.DOMContentLoaded,
                            conf.ForbiddenWaitSeconds,
                            ct
                        );
                        diagnostic.Step("WaitAutomaticContent", "body", 25000);
                        await WaitForAutomaticContentAsync(automaticPage);
                        diagnostic.Step("DiscoverLinks");
                        var discovered = AutomaticSource.Discover(
                            await automaticPage.ContentAsync(),
                            new Uri(automaticPage.Url)
                        );
                        foreach (var link in discovered.Links)
                        {
                            await policy.ValidateAsync(link, [new Uri(source.Url).IdnHost], ct);
                            automaticLinks.Add(link);
                        }

                        await SaveDiscoveryPageAsync(
                            new(
                                discovered
                                    .Links.Select(link => (TextRules.Hash(link), link))
                                    .ToList(),
                                discovered.Next,
                                n + 1
                            ),
                            ct
                        );
                        current = discovered.Next;
                    }

                    if (current is not null)
                        throw new InvalidOperationException(
                            "Automatic pagination limit reached; source needs review."
                        );
                    if (automaticLinks.Count == 0)
                        throw new InvalidOperationException(
                            "Automatic discovery found no reliable opportunity links; source needs review."
                        );
                    return automaticLinks.Select(link => (TextRules.Hash(link), link)).ToList();
                },
                ct
            );
        }

        var selector = source.Connector switch
        {
            ConnectorKind.Gpc => "a[href*='viewPublicAdvertismentInfoAll.gpc']",
            ConnectorKind.PalCareer => "a[href*='job-single.aspx?id=']",
            _ => conf.ListingLinkSelector,
        };
        if (string.IsNullOrWhiteSpace(selector))
            throw new InvalidOperationException("A listing link selector is required.");
        await using var context = await ContextAsync(source, ct);
        var page = await context.NewPageAsync();
        using var diagnostics = new CollectorDiagnosticSession(
            page,
            source,
            "Discovery",
            source.Url,
            configuration
        );
        return await diagnostics.RunAsync(
            async () =>
            {
                diagnostics.Step("NavigateListing", timeoutMs: 45000);
                await BrowserNavigation.OpenAsync(
                    page,
                    discovery?.Checkpoint.NextUrl is { Length: > 0 } resumeUrl
                        ? resumeUrl
                        : source.Url,
                    source.Connector == ConnectorKind.Gpc
                        ? WaitUntilState.DOMContentLoaded
                        : WaitUntilState.Commit,
                    conf.ForbiddenWaitSeconds,
                    ct
                );
                diagnostics.Step("WaitSourceSelector", conf.WaitSelector, 25000);
                if (conf.WaitSelector.Length > 0)
                    await page.Locator(conf.WaitSelector)
                        .First.WaitForAsync(new() { Timeout = 25000 });
                var found = new Dictionary<string, string>();
                if (source.Connector == ConnectorKind.Gpc)
                {
                    diagnostics.Step("ReadEmbeddedData");
                    var data = ExtractEmbeddedArray(await page.ContentAsync(), "datastr");
                    foreach (var entry in data.EnumerateArray())
                    {
                        var id = entry.GetProperty("vacancyNum").GetString();
                        if (string.IsNullOrWhiteSpace(id))
                            continue;
                        if (
                            entry.TryGetProperty("byDate", out var date)
                            && date.ValueKind == JsonValueKind.String
                            && GpcListingDate(date.GetString()) is { } deadline
                            && deadline < TextRules.PalestineToday()
                        )
                            continue;
                        found[id] = new Uri(
                            new Uri(source.Url),
                            "viewPublicAdvertismentInfoAll.gpc?vacancyNum="
                                + Uri.EscapeDataString(id)
                        ).ToString();
                    }
                }
                else
                {
                    var resumeBase = discovery?.Checkpoint.NextUrl is { Length: > 0 } baseUrl
                        ? baseUrl
                        : source.Url;
                    var replayClicks = discovery?.Checkpoint.ReplayClicks ?? 0;
                    for (var replay = 0; replay < replayClicks; replay++)
                    {
                        await page.Locator(selector).First.WaitForAsync(new() { Timeout = 20000 });
                        var control = page.Locator(conf.NextSelector).First;
                        if (await control.CountAsync() == 0 || !await control.IsEnabledAsync())
                            throw new ContentNeedsReviewException(
                                "Saved pagination cursor can no longer be restored; review the source before restarting discovery."
                            );
                        await AdvanceListingAsync(page, selector, control, ct);
                    }
                    for (
                        var n = discovery?.Checkpoint.CompletedPages ?? 0;
                        n < maxPages;
                        n++
                    )
                    {
                        ct.ThrowIfCancellationRequested();
                        diagnostics.Step("WaitListingSelector", selector, 20000);
                        await page.Locator(selector).First.WaitForAsync(new() { Timeout = 20000 });
                        diagnostics.Step("DiscoverLinks", selector);
                        var links = await page.Locator(selector)
                            .EvaluateAllAsync<string[]>("els => els.map(e => e.href)");
                        var pageEntries = new List<(string Id, string Url)>();
                        foreach (var link in links.Distinct())
                        {
                            var uri = await policy.ValidateAsync(
                                link,
                                conf.AllowedHosts.Append(new Uri(source.Url).IdnHost),
                                ct
                            );
                            var match = Regex.Match(uri.Query, "(?:^|[?&])id=([^&]+)");
                            var id = match.Success
                                ? Uri.UnescapeDataString(match.Groups[1].Value)
                                : TextRules.Hash(uri.ToString());
                            found[id] = uri.ToString();
                            pageEntries.Add((id, uri.ToString()));
                        }

                        if (conf.NextSelector.Length == 0)
                        {
                            await SaveDiscoveryPageAsync(new(pageEntries, null, n + 1), ct);
                            break;
                        }
                        var next = page.Locator(conf.NextSelector).First;
                        if (
                            await next.CountAsync() == 0
                            || !await next.IsVisibleAsync()
                            || !await next.IsEnabledAsync()
                        )
                        {
                            await SaveDiscoveryPageAsync(new(pageEntries, null, n + 1), ct);
                            break;
                        }
                        var href = await next.GetAttributeAsync("href");
                        var directNext =
                            !string.IsNullOrWhiteSpace(href)
                            && !href.StartsWith('#')
                            && Uri.TryCreate(new Uri(page.Url), href, out var nextUri)
                            && nextUri.ToString() != page.Url
                                ? nextUri!.ToString()
                                : null;
                        if (directNext is not null)
                        {
                            await policy.ValidateAsync(
                                directNext,
                                conf.AllowedHosts.Append(new Uri(source.Url).IdnHost),
                                ct
                            );
                            resumeBase = directNext;
                            replayClicks = 0;
                        }
                        else
                            replayClicks++;
                        await SaveDiscoveryPageAsync(
                            new(pageEntries, resumeBase, n + 1, replayClicks),
                            ct
                        );
                        if (n + 1 >= maxPages)
                            throw new InvalidOperationException(
                                "Pagination limit reached; increase the limit after reviewing this source."
                            );
                        if (directNext is not null)
                        {
                            // A real href is authoritative. Navigate without running click handlers;
                            // the next loop waits for the listing content in the new document.
                            diagnostics.Step("Pagination", conf.NextSelector, 45000);
                            await BrowserNavigation.OpenAsync(
                                page,
                                directNext,
                                WaitUntilState.DOMContentLoaded,
                                conf.ForbiddenWaitSeconds,
                                ct
                            );
                            if (conf.WaitSelector.Length > 0)
                            {
                                diagnostics.Step("WaitSourceSelector", conf.WaitSelector, 25000);
                                await page.Locator(conf.WaitSelector)
                                    .First.WaitForAsync(new() { Timeout = 25000 });
                            }
                        }
                        else
                        {
                            diagnostics.Step("Pagination", conf.NextSelector, 20000);
                            await AdvanceListingAsync(page, selector, next, ct);
                        }
                    }
                }

                if (found.Count == 0)
                    throw new InvalidOperationException(
                        "No listings found; possible protection page or changed source layout."
                    );
                return found.Select(x => (x.Key, x.Value)).ToList();
            },
            ct
        );
    }

    public async Task<string> FetchAsync(Source source, string url, CancellationToken ct) =>
        (await FetchBundleAsync(source, url, ct)).Text;

    public virtual async Task<ContentBundle> FetchBundleAsync(
        Source source,
        string url,
        CancellationToken ct
    )
    {
        if (source.Connector is ConnectorKind.JobsRss or ConnectorKind.ReliefWebRss)
        {
            return await CollectorDiagnosticFiles.WithoutPageAsync(
                source,
                "Detail",
                "ReadRss",
                source.Url,
                configuration,
                async () =>
                {
                    // One feed download serves every item of the run instead of one per item.
                    var entries = await OpportunityRss.ReadCachedAsync(source, policy, ct);
                    if (!entries.TryGetValue(url, out var content))
                        throw new ContentNeedsReviewException(
                            "Job is no longer in the RSS window; not evidence of closure."
                        );
                    return content;
                },
                ct
            );
        }

        var conf =
            JsonSerializer.Deserialize<SourceConfiguration>(source.ConfigurationJson) ?? new();
        await policy.ValidateAsync(
            url,
            conf.AllowedHosts.Append(new Uri(source.Url).IdnHost)
                .Concat(
                    source.Connector == ConnectorKind.UnrwaInspira ? new[] { "inspira.un.org" } : []
                ),
            ct
        );
        if (new Uri(url).AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return await CollectorDiagnosticFiles.WithoutPageAsync<ContentBundle>(
                source,
                "Detail",
                "ReadAttachments",
                url,
                configuration,
                async () =>
                {
                    var pdfText = await attachmentReader.ReadPdfAsync(
                        url,
                        conf.AllowedHosts.Append(new Uri(source.Url).IdnHost).ToArray(),
                        ct
                    );
                    return new(pdfText + "\nSOURCE LINKS:\n" + url, [url]);
                },
                ct
            );
        }

        await using var context = await ContextAsync(source, ct);
        var page = await context.NewPageAsync();
        using var diagnostics = new CollectorDiagnosticSession(
            page,
            source,
            "Detail",
            url,
            configuration
        );
        return await diagnostics.RunAsync<ContentBundle>(
            async () =>
            {
                diagnostics.Step("NavigateDetail", timeoutMs: 45000);
                await BrowserNavigation.OpenAsync(
                    page,
                    url,
                    WaitUntilState.Commit,
                    conf.ForbiddenWaitSeconds,
                    ct
                );
                diagnostics.Step("WaitSourceSelector", conf.WaitSelector, 20000);
                if (conf.WaitSelector.Length > 0)
                    await page.Locator(conf.WaitSelector)
                        .First.WaitForAsync(new() { Timeout = 20000 });
                diagnostics.Step("WaitDocumentReady", timeoutMs: 25000);
                await page.WaitForFunctionAsync(
                    "() => document.readyState !== 'loading'",
                    null,
                    new() { Timeout = 25000 }
                );
                string html;
                if (source.Connector == ConnectorKind.Automatic)
                {
                    diagnostics.Step("WaitAutomaticContent", "body", 25000);
                    await WaitForAutomaticContentAsync(page);
                    html = AutomaticSource.ContentHtml(await page.ContentAsync());
                }
                else
                {
                    diagnostics.Step("WaitContentSelector", conf.ContentSelector, 20000);
                    var content = page.Locator(conf.ContentSelector)
                        .Filter(new() { Visible = true }).First;
                    await content.WaitForAsync(new() { Timeout = 20000 });
                    diagnostics.Step("ReadContent", conf.ContentSelector);
                    html = await content.InnerHTMLAsync();
                }

                var doc = await new HtmlParser().ParseDocumentAsync(html, ct);
                foreach (
                    var e in doc.QuerySelectorAll("script,style,nav,header,footer,noscript,iframe")
                )
                    e.Remove();
                var text = Regex.Replace(doc.Body?.TextContent ?? "", "\\s+", " ").Trim();
                var hosts = conf.AllowedHosts.Append(new Uri(source.Url).IdnHost).ToArray();
                var links = new List<string> { url };
                foreach (var anchor in doc.QuerySelectorAll("a[href]"))
                {
                    var href = anchor.GetAttribute("href");
                    if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#'))
                        continue;
                    if (
                        Uri.TryCreate(new Uri(url), href, out var uri)
                        && uri.Scheme is "https" or "mailto" or "tel"
                    )
                        links.Add(uri.ToString());
                }

                var linkedPdfs = links
                    .Distinct()
                    .Where(x =>
                        Uri.TryCreate(x, UriKind.Absolute, out var uri)
                        && uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                    )
                    .ToArray();
                // Attachments on unapproved hosts can never pass the URL policy; skip them up front
                // instead of failing the item into repeated retries.
                var pdfs = linkedPdfs.Where(x => ApprovedHost(x, hosts)).ToArray();
                var skippedPdfs = linkedPdfs.Except(pdfs).ToArray();
                if (pdfs.Length > 3)
                    throw new ContentNeedsReviewException(
                        "More than 3 PDF attachments; configure or review this source."
                    );
                diagnostics.Step("ReadAttachments", timeoutMs: 40000);
                foreach (var pdf in pdfs)
                {
                    await policy.ValidateAsync(pdf, hosts, ct);
                    text +=
                        "\nATTACHMENT "
                        + pdf
                        + "\n"
                        + await attachmentReader.ReadPdfAsync(pdf, hosts, ct);
                }

                diagnostics.Step("ValidateContent");
                text += "\nSOURCE LINKS:\n" + string.Join('\n', links.Distinct().Take(50));
                foreach (var skipped in skippedPdfs.Take(10))
                    text +=
                        "\nNOTE: PDF attachment on a host not approved for this source was not read: "
                        + skipped;
                if (
                    text.Length < 80
                    || text.Contains("Verify you are human", StringComparison.OrdinalIgnoreCase)
                )
                    throw new InvalidOperationException(
                        "Incomplete detail content; requires source review."
                    );
                return new(SourceEvidence.Prepare(source.Connector, url, text), pdfs);
            },
            ct
        );
    }

    // GPC (Diwan) is a Palestinian site: numeric dates are day-first or ISO, never US month-first.
    // An unrecognised value never skips a listing; the detail page and AI decide instead.
    private static readonly string[] GpcDateFormats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.f",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy/MM/dd",
        "yyyy/M/d",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd/MM/yyyy HH:mm",
        "dd/MM/yyyy HH:mm:ss",
        "dd-MM-yyyy",
        "d-M-yyyy",
        "dd.MM.yyyy",
    ];

    public static DateOnly? GpcListingDate(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && DateTime.TryParseExact(
            value.Trim(),
            GpcDateFormats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var parsed
        )
            ? DateOnly.FromDateTime(parsed)
            : null;

    public static JsonElement ExtractEmbeddedArray(string html, string key)
    {
        var match = Regex.Match(html, Regex.Escape(key) + @"\s*:\s*\[");
        if (!match.Success)
            throw new InvalidOperationException("Embedded listing data not found.");
        var start = match.Index + match.Length - 1;
        var depth = 0;
        var quoted = false;
        var escaped = false;
        for (var i = start; i < html.Length; i++)
        {
            var c = html[i];
            if (quoted)
            {
                if (escaped)
                    escaped = false;
                else if (c == '\\')
                    escaped = true;
                else if (c == '"')
                    quoted = false;
                continue;
            }

            if (c == '"')
                quoted = true;
            else if (c == '[')
                depth++;
            else if (c == ']' && --depth == 0)
                return JsonDocument.Parse(html[start..(i + 1)]).RootElement.Clone();
        }

        throw new InvalidOperationException("Incomplete embedded JSON.");
    }

    private static async Task WaitForAutomaticContentAsync(IPage page)
    {
        await page.WaitForFunctionAsync(
            "() => document.readyState !== 'loading' && document.body && document.body.innerText.trim().length > 80",
            null,
            new() { Timeout = 25000 }
        );
        // Bounded DOM settling; readiness alone does not prove all asynchronous data has arrived.
        var previous = "";
        for (var i = 0; i < 8; i++)
        {
            var current = await page.Locator("body").InnerTextAsync();
            if (current == previous)
                return;
            previous = current;
            await Task.Delay(500);
        }
    }

    private static bool ApprovedHost(string url, IEnumerable<string> hosts) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && hosts.Any(h => string.Equals(h, uri.IdnHost, StringComparison.OrdinalIgnoreCase));

    public async ValueTask DisposeAsync()
    {
        // A shared host belongs to the worker container and is disposed at shutdown.
        if (ownedHost is not null)
            await ownedHost.DisposeAsync();
    }
}

public sealed class SqlRunLock : IAsyncDisposable
{
    private readonly SqlConnection connection;
    public bool Acquired { get; private set; }

    private readonly string resource;

    public SqlRunLock(string connectionString, string resource = "HiringPs.Pipeline")
    {
        connection = new(connectionString);
        this.resource = resource;
    }

    public async Task<bool> AcquireAsync(CancellationToken ct)
    {
        await connection.OpenAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "DECLARE @r int; EXEC @r=sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @r;";
        cmd.Parameters.AddWithValue("@resource", resource);
        Acquired = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) >= 0;
        return Acquired;
    }

    public async Task VerifyAsync(CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT APPLOCK_MODE('public',@resource,'Session')";
        cmd.Parameters.AddWithValue("@resource", resource);
        if (
            !Acquired
            || !string.Equals(
                Convert.ToString(await cmd.ExecuteScalarAsync(ct)),
                "Exclusive",
                StringComparison.Ordinal
            )
        )
            throw new InvalidOperationException("Pipeline lock lost; stop work.");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Acquired && connection.State == System.Data.ConnectionState.Open)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText =
                    "EXEC sp_releaseapplock @Resource=@resource, @LockOwner='Session';";
                cmd.Parameters.AddWithValue("@resource", resource);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            Acquired = false;
            await connection.DisposeAsync();
        }
    }
}
