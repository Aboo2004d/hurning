using System.Text.Json;
using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>يجمع صفحات المصدر وأدلتها باستخدام Playwright دون تنفيذ النشر أو الدفع.</summary>
public partial class BrowserCollector
{
    /// <summary>يفحص الصفحة لمساعدة إعداد المصدر دون اعتماد إعدادات غير مراجعة.</summary>
    public virtual async Task<SourceSetupSnapshot> InspectForSetupAsync(
        string url,
        CancellationToken ct
    )
    {
        SourceSetupRules.Website(url);
        if (url == OpportunityRss.JobsUrl || url == OpportunityRss.ReliefUrl)
        {
            var rss = new Source
            {
                Url = url,
                Connector =
                    url == OpportunityRss.JobsUrl
                        ? ConnectorKind.JobsRss
                        : ConnectorKind.ReliefWebRss,
            };
            var feed = await OpportunityRss.ReadAsync(rss, policy, ct);
            return new(
                url,
                "RSS",
                string.Join('\n', feed.Values.Take(3).Select(x => x.Text)).Truncate(12000),
                "",
                feed.Keys.Where(x => x.Length <= 1000)
                    .Take(200)
                    .Select(x => new SourceSetupLink(x, "Announcement"))
                    .ToArray()
            );
        }
        var source = new Source { Url = url, Connector = ConnectorKind.Automatic };
        await using var context = await ContextAsync(source, ct, setup: true);
        var page = await context.NewPageAsync();
        await OpenSetupPageAsync(page, url, ct);
        return SourceSetupRules.Snapshot(await page.ContentAsync(), page.Url);
    }

    /// <summary>يعرض عينة من نتائج إعدادات المصدر قبل حفظها.</summary>
    public virtual async Task<SourceSetupPreview> PreviewSetupAsync(
        Source source,
        CancellationToken ct
    )
    {
        if (source.Connector is ConnectorKind.JobsRss or ConnectorKind.ReliefWebRss)
        {
            var feed = await OpportunityRss.ReadAsync(source, policy, ct);
            if (feed.Count == 0)
                throw new InvalidOperationException("SourceSetupNoLinks");
            return new(feed.Keys.Take(10).ToArray(), null, feed.First().Value.Text.Truncate(1200));
        }
        var conf = JsonSerializer.Deserialize<SourceConfiguration>(source.ConfigurationJson)!;
        await using var context = await ContextAsync(source, ct, setup: true);
        var page = await context.NewPageAsync();
        await OpenSetupPageAsync(page, source.Url, ct);
        string[] links;
        string? next;
        if (source.Connector == ConnectorKind.Automatic)
        {
            (links, next) = AutomaticSource.Discover(await page.ContentAsync(), new Uri(page.Url));
        }
        else
        {
            links = await page.Locator(conf.ListingLinkSelector)
                .EvaluateAllAsync<string[]>("els => els.map(e => e.href).filter(Boolean)");
            next = null;
            if (conf.NextSelector.Length == 0)
            {
                var automatic = AutomaticSource.Discover(
                    await page.ContentAsync(),
                    new Uri(page.Url)
                );
                if (automatic.Next is not null)
                    throw new InvalidOperationException("SourceSetupPaginationMissing");
            }
            if (conf.NextSelector.Length > 0)
            {
                var control = page.Locator(conf.NextSelector).First;
                if (await control.CountAsync() == 0)
                    throw new InvalidOperationException("SourceSetupPaginationMissing");
                if (await control.IsEnabledAsync())
                {
                    var href = await control.GetAttributeAsync("href");
                    if (
                        string.IsNullOrWhiteSpace(href)
                        || href.StartsWith('#')
                        || !Uri.TryCreate(new Uri(page.Url), href, out var target)
                    )
                        throw new InvalidOperationException("SourceSetupInteractivePagination");
                    next = target.AbsoluteUri;
                }
            }
        }
        links = links.Distinct().ToArray();
        if (links.Length is 0 or > 250 || links.Any(x => x.Length > 1000))
            throw new InvalidOperationException("SourceSetupNoLinks");
        foreach (var url in links)
        {
            await policy.ValidateAsync(url, [new Uri(source.Url).IdnHost], ct);
            if (
                new Uri(url).GetLeftPart(UriPartial.Path)
                    == new Uri(source.Url).GetLeftPart(UriPartial.Path)
                && new Uri(url).Query == new Uri(source.Url).Query
            )
                throw new InvalidOperationException("SourceSetupNoLinks");
        }
        if (next is not null)
        {
            if (new Uri(next) == new Uri(page.Url))
                throw new InvalidOperationException("SourceSetupPaginationMissing");
            await policy.ValidateAsync(next, [new Uri(source.Url).IdnHost], ct);
        }
        // Use the production detail reader, including its content completeness checks.
        var sample = await FetchBundleAsync(source, links[0], ct);
        return new(links.Take(10).ToArray(), next, sample.Text.Truncate(1200));
    }

    /// <summary>يفتح صفحة إعداد المصدر مع تطبيق سياسة العناوين والمهلة.</summary>
    private static async Task OpenSetupPageAsync(IPage page, string url, CancellationToken ct)
    {
        // Validate the navigation response without waiting for every parser-blocking resource.
        await BrowserNavigation.OpenAsync(page, url, WaitUntilState.Commit, 5, ct);
        try
        {
            // Preserve normal readiness and settling for pages that load normally.
            await WaitForAutomaticContentAsync(page);
        }
        catch (TimeoutException) when (!ct.IsCancellationRequested)
        {
            // Some pages already contain useful rendered links while DOMContentLoaded is delayed.
            // This only permits an inspection sample, never automatic source acceptance.
            var usable = await page.EvaluateAsync<bool>(
                """
                () => document.body && document.body.innerText.trim().length >= 300
                    && document.querySelectorAll('a[href]').length >= 3
                    && !/just a moment|access denied|request blocked|verify you are human|403 error/i
                        .test(document.title + ' ' + document.body.innerText.slice(0,500))
                """
            );
            if (!usable) throw;
            string? previous = null;
            var stable = 0;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                var current = await page.EvaluateAsync<string>(
                    "() => document.body.innerText + '\\n' + Array.from(document.querySelectorAll('a[href]')).map(a=>a.href).join('|')");
                stable = current == previous ? stable + 1 : 0;
                if (stable >= 3) return;
                previous = current;
                await Task.Delay(500, ct);
            }
            throw new TimeoutException("Source setup content did not stabilize; no settings were verified.");
        }
    }
}
