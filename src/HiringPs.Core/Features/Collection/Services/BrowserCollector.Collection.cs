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

/// <summary>مكون BrowserCollector ضمن مجال Collection؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
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

    /// <summary>ينفذ ContextAsync وفق قواعد BrowserCollector.Collection الحالية دون تغيير عقد المستدعين.</summary>
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

    /// <summary>ينفذ CreateContextAsync وفق قواعد BrowserCollector.Collection الحالية دون تغيير عقد المستدعين.</summary>
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

    /// <summary>ينفذ RouteAsync وفق قواعد BrowserCollector.Collection الحالية دون تغيير عقد المستدعين.</summary>
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

    /// <summary>ينفذ DiscoverySession وفق قواعد BrowserCollector.Collection الحالية دون تغيير عقد المستدعين.</summary>
    private sealed class DiscoverySession(
        DiscoveryCheckpoint checkpoint,
        Func<DiscoveryBatch, CancellationToken, Task> save
    )
    {
        public DiscoveryCheckpoint Checkpoint { get; } = checkpoint;
        public Func<DiscoveryBatch, CancellationToken, Task> Save { get; } = save;
        public bool Saved { get; set; }
    }

    /// <summary>ينفذ DiscoverResumableAsync وفق قواعد BrowserCollector.Collection الحالية دون تغيير عقد المستدعين.</summary>
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

    /// <summary>ينفذ SaveDiscoveryPageAsync وفق قواعد BrowserCollector.Collection الحالية دون تغيير عقد المستدعين.</summary>
    private async Task SaveDiscoveryPageAsync(DiscoveryBatch batch, CancellationToken ct)
    {
        if (discovery is null)
            return;
        await discovery.Save(batch, ct);
        discovery.Saved = true;
    }

    /// <summary>ينفذ AdvanceListingAsync وفق قواعد BrowserCollector.Collection الحالية دون تغيير عقد المستدعين.</summary>
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

    /// <summary>يكتشف المصدر ويثبت الدفعات ويحدث الصحة والجدولة تحت قفل الدورة.</summary>
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

    /// <summary>ينفذ FetchAsync وفق قواعد BrowserCollector.Collection الحالية دون تغيير عقد المستدعين.</summary>
    public async Task<string> FetchAsync(Source source, string url, CancellationToken ct) =>
        (await FetchBundleAsync(source, url, ct)).Text;

    /// <summary>ينفذ FetchBundleAsync وفق قواعد BrowserCollector.Collection الحالية دون تغيير عقد المستدعين.</summary>
    public virtual async Task<ContentBundle> FetchBundleAsync(
        Source source,
        string url,
        CancellationToken ct
    )
    {
        if (source.Connector == ConnectorKind.ReliefWebRss)
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
                string html;
                if (source.Connector == ConnectorKind.UnrwaInspira)
                {
                    var seconds = Math.Clamp(configuration.GetValue<int?>("Browser:InspiraDetailTimeoutSeconds") ?? 45, 15, 90);
                    diagnostics.Step("WaitInspiraJobContent", "body", seconds * 1000);
                    html = await InspiraJobPage.ReadAsync(page, conf.AllowedHosts.Append(new Uri(source.Url).IdnHost)
                        .Append("inspira.un.org"), TimeSpan.FromSeconds(seconds), ct);
                }
                else
                {
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
                    if (source.Connector == ConnectorKind.JobsRss && conf.ContentSelector is "" or "body")
                    {
                        diagnostics.Step("WaitJobDetail", "body", 25000);
                        await WaitForAutomaticContentAsync(page);
                        html = RssJobPage.ContentHtml(await page.ContentAsync());
                    }
                    else if (source.Connector == ConnectorKind.Automatic)
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

                }

                var doc = await new HtmlParser().ParseDocumentAsync(html, ct);
                foreach (
                    var e in doc.QuerySelectorAll(source.Connector == ConnectorKind.JobsRss
                        ? "script,style,nav,footer,noscript,iframe" : "script,style,nav,header,footer,noscript,iframe")
                )
                    e.Remove();
                var text = SourceHtmlText.Read(doc);
                if (source.Connector == ConnectorKind.JobsRss && RssJobPage.IsSummary(text))
                    throw new ContentNeedsReviewException("Original job detail page is incomplete; RSS summary is not sufficient for extraction.");
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

    /// <summary>ينفذ GpcListingDate وفق قواعد BrowserCollector.Collection الحالية دون تغيير عقد المستدعين.</summary>
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

    /// <summary>ينفذ ExtractEmbeddedArray وفق قواعد BrowserCollector.Collection الحالية دون تغيير عقد المستدعين.</summary>
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

    /// <summary>ينفذ WaitForAutomaticContentAsync وفق قواعد BrowserCollector.Collection الحالية دون تغيير عقد المستدعين.</summary>
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

    /// <summary>ينفذ ApprovedHost وفق قواعد BrowserCollector.Collection الحالية دون تغيير عقد المستدعين.</summary>
    private static bool ApprovedHost(string url, IEnumerable<string> hosts) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && hosts.Any(h => string.Equals(h, uri.IdnHost, StringComparison.OrdinalIgnoreCase));

    /// <summary>يغلق الموارد التي يملكها المكون مع احترام ملكية المتصفح المشتركة.</summary>
    public async ValueTask DisposeAsync()
    {
        // A shared host belongs to the worker container and is disposed at shutdown.
        if (ownedHost is not null)
            await ownedHost.DisposeAsync();
    }
}
