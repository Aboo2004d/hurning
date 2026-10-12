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
}
public class UrlPolicy
{
    public async Task<Uri> ValidateAsync(string url, IEnumerable<string> hosts, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) throw new InvalidOperationException("Only public HTTPS URLs without credentials are allowed.");
        if (!hosts.Any(h => string.Equals(h, uri.IdnHost, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Host is not approved for this source.");
        var addresses = await Dns.GetHostAddressesAsync(uri.IdnHost, ct);
        if (addresses.Length == 0 || addresses.Any(IsPrivate)) throw new InvalidOperationException("Private/reserved network targets are not allowed.");
        return uri;
    }
    public static bool IsPrivate(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return true;
        var b = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork) return b[0] is 0 or 10 or 127 or >= 224 || (b[0] == 169 && b[1] == 254) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] >= 64 && b[1] <= 127) || (b[0] == 198 && b[1] is 18 or 19);
        return ip.IsIPv6LinkLocal || ip.IsIPv6Multicast || ip.IsIPv6SiteLocal || (b[0] & 0xfe) == 0xfc;
    }
}
public class BrowserCollector(UrlPolicy policy, IConfiguration configuration,AttachmentReader attachmentReader) : IAsyncDisposable
{
    private IPlaywright? playwright; private IBrowser? browser;
    private async Task<IBrowserContext> ContextAsync(Source source, CancellationToken ct)
    {
        var conf = JsonSerializer.Deserialize<SourceConfiguration>(source.ConfigurationJson) ?? new();
        var hosts = conf.AllowedHosts.Append(new Uri(source.Url).IdnHost).ToArray();
        await policy.ValidateAsync(source.Url, hosts, ct);
        playwright ??= await Playwright.CreateAsync();
        browser ??= await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = configuration["Browser:Channel"] is { Length: > 0 } channel ? channel : null });
        var context = await browser.NewContextAsync(new() { AcceptDownloads = false, IgnoreHTTPSErrors = false, ServiceWorkers = ServiceWorkerPolicy.Block });
        context.SetDefaultTimeout(20000);
        await context.RouteAsync("**/*", async route =>
        {
            try
            {
                if (route.Request.ResourceType is "image" or "font" or "media") { await route.AbortAsync(); return; }
                await policy.ValidateAsync(route.Request.Url, hosts, ct);
                await route.ContinueAsync();
            }
            catch { await route.AbortAsync(); }
        });
        return context;
    }
    public virtual async Task<List<(string Id, string Url)>> DiscoverAsync(Source source, CancellationToken ct)
    {
        if (!source.Enabled || !source.UsageApproved) throw new InvalidOperationException("Source must be enabled and usage approved.");
        if (source.Connector == ConnectorKind.ReliefWeb) throw new InvalidOperationException("ReliefWeb requires an approved appname and a tested API adapter; this source remains pending.");
        var conf = JsonSerializer.Deserialize<SourceConfiguration>(source.ConfigurationJson) ?? new();
        if (source.Connector == ConnectorKind.Automatic)
        {
            await using var automaticContext = await ContextAsync(source, ct);
            var automaticPage = await automaticContext.NewPageAsync();
            var visited = new HashSet<string>(); var automaticLinks = new HashSet<string>();
            string? current = source.Url;
            for (var n = 0; current is not null && n < Math.Clamp(conf.MaxPages, 1, 50); n++)
            {
                ct.ThrowIfCancellationRequested();
                if (!visited.Add(current)) throw new InvalidOperationException("Automatic pagination loop; source needs review.");
                await policy.ValidateAsync(current, [new Uri(source.Url).IdnHost], ct);
                var result = await automaticPage.GotoAsync(current, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45000 });
                if (result is null || result.Status >= 400) throw new InvalidOperationException($"Source returned HTTP {result?.Status}; collection stopped.");
                await WaitForAutomaticContentAsync(automaticPage);
                var discovered = AutomaticSource.Discover(await automaticPage.ContentAsync(), new Uri(automaticPage.Url));
                foreach (var link in discovered.Links) { await policy.ValidateAsync(link, [new Uri(source.Url).IdnHost], ct); automaticLinks.Add(link); }
                current = discovered.Next;
            }
            if (current is not null) throw new InvalidOperationException("Automatic pagination limit reached; source needs review.");
            if (automaticLinks.Count == 0) throw new InvalidOperationException("Automatic discovery found no reliable opportunity links; source needs review.");
            return automaticLinks.Select(link => (TextRules.Hash(link), link)).ToList();
        }
        var selector = source.Connector switch { ConnectorKind.Gpc => "a[href*='viewPublicAdvertismentInfoAll.gpc']", ConnectorKind.PalCareer => "a[href*='job-single.aspx?id=']", _ => conf.ListingLinkSelector };
        if (string.IsNullOrWhiteSpace(selector)) throw new InvalidOperationException("A listing link selector is required.");
        await using var context = await ContextAsync(source, ct); var page = await context.NewPageAsync();
        var response = await page.GotoAsync(source.Url, new() { WaitUntil = source.Connector == ConnectorKind.Gpc ? WaitUntilState.DOMContentLoaded : WaitUntilState.Commit, Timeout = 45000 });
        if (response is null || response.Status >= 400) throw new InvalidOperationException($"Source returned HTTP {response?.Status}; collection stopped.");
        if (conf.WaitSelector.Length > 0) await page.Locator(conf.WaitSelector).First.WaitForAsync(new() { Timeout = 25000 });
        var found = new Dictionary<string, string>();
        if (source.Connector == ConnectorKind.Gpc)
        {
            var data = ExtractEmbeddedArray(await page.ContentAsync(), "datastr");
            foreach (var entry in data.EnumerateArray())
            {
                var id = entry.GetProperty("vacancyNum").GetString(); if (string.IsNullOrWhiteSpace(id)) continue;
                if (entry.TryGetProperty("byDate", out var date) && DateTime.TryParse(date.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var deadline) && DateOnly.FromDateTime(deadline) < TextRules.PalestineToday()) continue;
                found[id] = new Uri(new Uri(source.Url), "viewPublicAdvertismentInfoAll.gpc?vacancyNum=" + Uri.EscapeDataString(id)).ToString();
            }
        }
        else
        {
            for (var n = 0; n < Math.Clamp(conf.MaxPages, 1, 50); n++)
            {
                ct.ThrowIfCancellationRequested(); await page.Locator(selector).First.WaitForAsync(new() { Timeout = 20000 });
                var links = await page.Locator(selector).EvaluateAllAsync<string[]>("els => els.map(e => e.href)");
                foreach (var link in links.Distinct())
                {
                    var uri = await policy.ValidateAsync(link, conf.AllowedHosts.Append(new Uri(source.Url).IdnHost), ct);
                    var match = Regex.Match(uri.Query, "(?:^|[?&])id=([^&]+)");
                    var id = match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : TextRules.Hash(uri.ToString()); found[id] = uri.ToString();
                }
                if (conf.NextSelector.Length == 0) break;
                var next = page.Locator(conf.NextSelector).First;
                if (await next.CountAsync() == 0 || !await next.IsVisibleAsync() || !await next.IsEnabledAsync()) break;
                if (n + 1 >= conf.MaxPages) throw new InvalidOperationException("Pagination limit reached; increase the limit after reviewing this source.");
                var before = string.Join('|', links); await next.ClickAsync();
                await page.WaitForFunctionAsync("([s, before]) => Array.from(document.querySelectorAll(s)).map(e=>e.href).join('|') !== before", new[] { selector, before }, new() { Timeout = 20000 });
            }
        }
        if (found.Count == 0) throw new InvalidOperationException("No listings found; possible protection page or changed source layout.");
        return found.Select(x => (x.Key, x.Value)).ToList();
    }
    public async Task<string> FetchAsync(Source source,string url,CancellationToken ct)=>(await FetchBundleAsync(source,url,ct)).Text;
    public virtual async Task<ContentBundle> FetchBundleAsync(Source source, string url, CancellationToken ct)
    {
        var conf = JsonSerializer.Deserialize<SourceConfiguration>(source.ConfigurationJson) ?? new();
        await policy.ValidateAsync(url, conf.AllowedHosts.Append(new Uri(source.Url).IdnHost), ct);
        if(new Uri(url).AbsolutePath.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase))
        {
            var pdfText=await attachmentReader.ReadPdfAsync(url,conf.AllowedHosts.Append(new Uri(source.Url).IdnHost).ToArray(),ct);
            return new(pdfText+"\nSOURCE LINKS:\n"+url,[url]);
        }
        await using var context = await ContextAsync(source, ct); var page = await context.NewPageAsync();
        var response = await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.Commit, Timeout = 45000 });
        if (response is null || response.Status >= 400) throw new InvalidOperationException($"Detail HTTP {response?.Status}; not evidence of closure.");
        if (conf.WaitSelector.Length > 0) await page.Locator(conf.WaitSelector).First.WaitForAsync(new() { Timeout = 20000 });
        await page.WaitForFunctionAsync("() => document.readyState !== 'loading'", null, new() { Timeout = 25000 });
        string html;
        if (source.Connector == ConnectorKind.Automatic)
        {
            await WaitForAutomaticContentAsync(page);
            html = AutomaticSource.ContentHtml(await page.ContentAsync());
        }
        else
        {
            await page.Locator(conf.ContentSelector).First.WaitForAsync();
            html = await page.Locator(conf.ContentSelector).First.InnerHTMLAsync();
        }
        var doc = await new HtmlParser().ParseDocumentAsync(html, ct);
        foreach (var e in doc.QuerySelectorAll("script,style,nav,header,footer,noscript,iframe")) e.Remove();
        var text = Regex.Replace(doc.Body?.TextContent ?? "", "\\s+", " ").Trim();
        var hosts=conf.AllowedHosts.Append(new Uri(source.Url).IdnHost).ToArray();
        var links=new List<string>();
        foreach(var anchor in doc.QuerySelectorAll("a[href]"))
        {
            var href=anchor.GetAttribute("href");if(string.IsNullOrWhiteSpace(href)||href.StartsWith('#'))continue;
            if(Uri.TryCreate(new Uri(url),href,out var uri)&&uri.Scheme is "https" or "mailto" or "tel")links.Add(uri.ToString());
        }
        var pdfs=links.Distinct().Where(x=>Uri.TryCreate(x,UriKind.Absolute,out var uri)&&uri.AbsolutePath.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase)).ToArray();
        if(pdfs.Length>3)throw new ContentNeedsReviewException("More than 3 PDF attachments; configure or review this source.");
        foreach(var pdf in pdfs){await policy.ValidateAsync(pdf,hosts,ct);text+="\nATTACHMENT "+pdf+"\n"+await attachmentReader.ReadPdfAsync(pdf,hosts,ct);}
        text+="\nSOURCE LINKS:\n"+string.Join('\n',links.Distinct().Take(50));
        if (text.Length < 80 || text.Contains("Verify you are human", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Incomplete detail content; requires source review.");
        if (text.Length > 40000) throw new InvalidOperationException("Detail exceeds processing limit; configure a precise content selector.");
        return new(text,pdfs);
    }
    public static JsonElement ExtractEmbeddedArray(string html, string key)
    {
        var match = Regex.Match(html, Regex.Escape(key) + @"\s*:\s*\["); if (!match.Success) throw new InvalidOperationException("Embedded listing data not found.");
        var start = match.Index + match.Length - 1; var depth = 0; var quoted = false; var escaped = false;
        for (var i = start; i < html.Length; i++)
        {
            var c = html[i]; if (quoted) { if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quoted = false; continue; }
            if (c == '"') quoted = true; else if (c == '[') depth++; else if (c == ']' && --depth == 0) return JsonDocument.Parse(html[start..(i + 1)]).RootElement.Clone();
        }
        throw new InvalidOperationException("Incomplete embedded JSON.");
    }
    private static async Task WaitForAutomaticContentAsync(IPage page)
    {
        await page.WaitForFunctionAsync("() => document.readyState !== 'loading' && document.body && document.body.innerText.trim().length > 80", null, new() { Timeout = 25000 });
        // Bounded DOM settling; readiness alone does not prove all asynchronous data has arrived.
        var previous = "";
        for (var i = 0; i < 8; i++)
        {
            var current = await page.Locator("body").InnerTextAsync();
            if (current == previous) return;
            previous = current; await Task.Delay(500);
        }
    }
    public async ValueTask DisposeAsync() { if (browser is not null) await browser.DisposeAsync(); playwright?.Dispose(); }
}
public sealed class SqlRunLock : IAsyncDisposable
{
    private readonly SqlConnection connection; public bool Acquired { get; private set; }
    public SqlRunLock(string connectionString) => connection = new(connectionString);
    public async Task<bool> AcquireAsync(CancellationToken ct)
    {
        await connection.OpenAsync(ct); await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DECLARE @r int; EXEC @r=sp_getapplock @Resource='HiringPs.Pipeline', @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @r;";
        Acquired = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) >= 0; return Acquired;
    }
    public async Task VerifyAsync(CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand(); cmd.CommandText = "SELECT APPLOCK_MODE('public','HiringPs.Pipeline','Session')";
        if (!Acquired || !string.Equals(Convert.ToString(await cmd.ExecuteScalarAsync(ct)), "Exclusive", StringComparison.Ordinal)) throw new InvalidOperationException("Pipeline lock lost; stop work.");
    }
    public async ValueTask DisposeAsync()
    {
        try { if (Acquired && connection.State == System.Data.ConnectionState.Open) { await using var cmd = connection.CreateCommand(); cmd.CommandText = "EXEC sp_releaseapplock @Resource='HiringPs.Pipeline', @LockOwner='Session';"; await cmd.ExecuteNonQueryAsync(); } }
        finally { Acquired = false; await connection.DisposeAsync(); }
    }
}
