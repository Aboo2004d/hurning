using System.Net;
using System.Text.Json;
using HiringPs.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public static class JsonApiChecks
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool ok, string name)
        {
            if (!ok) throw new Exception(name);
            count++;
            Console.WriteLine("PASS " + name);
        }
        void Reject(Action action, string name)
        {
            try { action(); } catch (Exception ex) when (ex is InvalidOperationException or ContentNeedsReviewException)
            { Check(true, name); return; }
            throw new Exception("Accepted: " + name);
        }
        async Task RejectAsync(Func<Task> action, string name)
        {
            try { await action(); } catch (Exception ex) when (ex is InvalidOperationException or CollectorDiagnosticException or HttpRequestException)
            { Check(true, name); return; }
            throw new Exception("Accepted: " + name);
        }
        const string url = "https://jobmatch.ps/jobs?country=1000000055&page=1&per_page=5";
        var source = SourceApiProfiles.ForWebsite(url)!;
        source.Enabled = source.UsageApproved = true;
        var conf = JsonApiReader.Configuration(source);
        var api = conf.Api!;
        Check(source.Connector == ConnectorKind.JsonApi && (int)source.Connector == 8, "New connector preserves existing enum numbers");
        Check(JsonApiReader.PageUrl(api, 2) == "https://api.jobmatch.ps/api/v1/jobs?country=1000000055&page=2&per_page=5",
            "Country filter and page size survive pagination");
        Check(SourceApiProfiles.ForWebsite("https://jobmatch.ps/") is not null
            && SourceApiProfiles.ForWebsite("https://jobmatch.ps.evil.test/jobs") is null,
            "Preset recognizes exact tested source host only");
        Reject(() => SourceApiProfiles.ForWebsite(url + "&country_id=123"), "Unverified query filters are not silently dropped");
        Reject(() => SourceApiProfiles.ForWebsite(url.Replace("1000000055", "123")), "Untested country filter is not presented as verified");
        var page = Page(1, 2, (11, "first"), (11, "first"), (12, "second"));
        using (var json = JsonDocument.Parse(page))
        {
            var parsed = JsonApiReader.ParsePage(json.RootElement, api, 1);
            Check(parsed.Items.Count == 2 && parsed.Items[0] == ("11", "https://jobmatch.ps/jobs/first") && parsed.HasNext,
                "Stable numeric identities deduplicate repeated rows independently of slugs");
            Reject(() => JsonApiReader.ParsePage(json.RootElement, api, 2), "Wrong current-page metadata fails instead of looping");
        }
        foreach (var bad in new[] { "{\"success\":false}",
            Page(1, 2), Page(1, 0, (11,"first")), Page(1, 1, (11,"../private")) })
        {
            using var json = JsonDocument.Parse(bad);
            Reject(() => JsonApiReader.ParsePage(json.RootElement, api, 1), "Malformed/failed API page cannot complete discovery: " + bad[..Math.Min(32,bad.Length)]);
        }
        using (var json = JsonDocument.Parse(Page(1, 1)))
            Check(JsonApiReader.ParsePage(json.RootElement, api, 1) is { HasNext: false, Items.Count: 0 },
                "Explicit empty first/final page is a valid empty listing");
        var detail = Detail(11, "first");
        using (var json = JsonDocument.Parse(detail))
        {
            var bundle = JsonApiReader.ParseDetail(json.RootElement, api, "https://jobmatch.ps/jobs/first", expectedSlug: "first");
            Check(bundle.Text.Contains("Full announcement") && bundle.Text.Contains("https://example.org/apply")
                && bundle.Text.Contains("2030-12-01") && !bundle.Text.Contains("SECRET_SCRIPT") && !bundle.Text.Contains("<p>"),
                "Full detail retains dates, source links and readable body, removes scripts and markup");
            Reject(() => JsonApiReader.ParseDetail(json.RootElement, api, url, expectedSlug: "different"), "Wrong detail identity is rejected");
        }
        using (var json = JsonDocument.Parse(Detail(11, "first", "tiny")))
            Reject(() => JsonApiReader.ParseDetail(json.RootElement, api, url), "A list summary cannot substitute for full detail content");
        var altered = JsonSerializer.Deserialize<SourceConfiguration>(source.ConfigurationJson)!;
        altered.Api!.DetailUrlTemplate = "https://other.example/jobs/{slug}";
        Reject(() => JsonApiReader.Validate(source.Url, altered), "Unapproved API host is rejected");
        altered.Api.DetailUrlTemplate = "https://api.jobmatch.ps/api/v1/jobs?slug={slug}";
        Reject(() => JsonApiReader.Validate(source.Url, altered), "Identity cannot be inserted into arbitrary query parameters");
        altered.Api.DetailUrlTemplate = "https://api.jobmatch.ps/api/v1/jobs/{id}";
        Reject(() => JsonApiReader.Validate(source.Url, altered), "Unresolvable detail identity is rejected at configuration time");
        Check(!SourceHealth.TracksListing(ConnectorKind.JsonApi), "Rolling API window absence cannot trigger closure or missing-listing review");

        // Same reader, different envelope and field names: no website-specific adapter code.
        var other = new SourceConfiguration { AllowedHosts = ["example.org"], Api = new()
        {
            ListingUrlTemplate = "https://example.org/api/vacancies?page={page}",
            ItemUrlTemplate = "https://example.org/vacancies/{id}", DetailUrlTemplate = "https://example.org/api/vacancies/{id}",
            SuccessPath = "", ItemsPath = "results", DetailPath = "vacancy", IdPath = "reference",
            CurrentPagePath = "paging.page", LastPagePath = "paging.pages", ContentPath = "body",
            Fields = [new("Title", "name"), new("Description", "body", true)]
        } };
        JsonApiReader.Validate("https://example.org/vacancies", other);
        using (var json = JsonDocument.Parse("""{"results":[{"reference":"ref-1"}],"paging":{"page":1,"pages":1}}"""))
            Check(JsonApiReader.ParsePage(json.RootElement, other.Api, 1).Items.Single().Url == "https://example.org/vacancies/ref-1",
                "A second API schema works through configuration alone");

        // No real HTTP/AI/DB requests. UrlPolicy still resolves the public source host.
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["Diagnostics:CollectorPath"] = Path.Combine(Path.GetTempPath(), "hiring-api-checks-" + Guid.NewGuid().ToString("N")) }).Build();
        using var transport = new FixtureHandler();
        using var http = new HttpClient(transport);
        using var reader = new JsonApiReader(new UrlPolicy(), http);
        await using var browser = new BrowserCollector(new UrlPolicy(), config, new AttachmentReader(new UrlPolicy(), config), apiReader: reader);
        var checkpoint = new DiscoveryCheckpoint { SourceId = 1, NextUrl = source.Url };
        var batches = new List<DiscoveryBatch>();
        await browser.DiscoverResumableAsync(source, checkpoint, (batch, _) =>
        {
            batches.Add(batch); checkpoint.CompletedPages = batch.CompletedPages;
            checkpoint.NextUrl = batch.NextUrl ?? ""; checkpoint.Completed = batch.NextUrl is null;
            return Task.CompletedTask;
        }, default);
        Check(batches.Count == 2 && batches[0].NextUrl!.Contains("page=2") && batches[1].NextUrl is null
            && checkpoint.CompletedPages == 2 && transport.Urls.Count == 2,
            "Production discovery saves one resumable batch per API page");
        var fetched = await browser.FetchBundleAsync(source, "https://jobmatch.ps/jobs/first", default);
        Check(fetched.Text.Contains("Full announcement") && transport.Urls.Last() == "https://api.jobmatch.ps/api/v1/jobs/first",
            "Production detail fetching uses API rather than the HTML frontend");
        transport.FailSecondPage = true;
        checkpoint = new() { NextUrl = source.Url };
        await RejectAsync(async () => await browser.DiscoverResumableAsync(source, checkpoint, (batch, _) =>
        {
            checkpoint.CompletedPages = batch.CompletedPages;
            checkpoint.NextUrl = batch.NextUrl ?? ""; checkpoint.Completed = batch.NextUrl is null;
            return Task.CompletedTask;
        }, default), "A later API page failure does not complete discovery");
        Check(checkpoint.CompletedPages == 1 && !checkpoint.Completed && checkpoint.NextUrl.Contains("page=2"),
            "Successfully saved page remains resumable after the next page fails");
        transport.FailSecondPage = false;
        transport.Urls.Clear();
        checkpoint = new() { CompletedPages = 1, NextUrl = JsonApiReader.PageUrl(api, 2) };
        await browser.DiscoverResumableAsync(source, checkpoint, (_, _) => Task.CompletedTask, default);
        Check(transport.Urls.Count == 1 && transport.Urls[0].Contains("page=2"), "Restart resumes at the next saved API page");
        transport.Urls.Clear();
        checkpoint.Completed = true;
        await browser.DiscoverResumableAsync(source, checkpoint, (_, _) => Task.CompletedTask, default);
        Check(transport.Urls.Count == 0, "A completed checkpoint makes no further requests");
        var assistant = new SourceSetupAssistant(http, browser, config);
        var result = await assistant.SuggestAsync(url, "ar", default);
        Check(result.Connector == 8 && result.Verified && result.Preview.SampleText.Contains("Full announcement")
            && transport.Urls.All(x => x.StartsWith("https://api.jobmatch.ps/")),
            "Setup uses the same real preview without Chromium or configured AI credentials");
        transport.Mode = "html";
        await RejectAsync(async () => await reader.ReadPageAsync(source, 1, default), "HTML protection response cannot be mistaken for an empty API listing");
        transport.Mode = "error";
        await RejectAsync(async () => await reader.ReadPageAsync(source, 1, default), "HTTP 500 cannot be interpreted as a successful empty API listing");
        try { await browser.FetchBundleAsync(source, "https://jobmatch.ps/jobs/first", default); throw new Exception("Accepted HTTP failure"); }
        catch (CollectorDiagnosticException ex)
        { Check(ex.Diagnostic.HttpStatus == 500, "API HTTP status is retained in the existing diagnostic report"); }
        transport.Mode = "redirect";
        await RejectAsync(async () => await reader.ReadPageAsync(source, 1, default), "Redirect cannot be silently followed to another endpoint");
        transport.Mode = "large";
        await RejectAsync(async () => await reader.ReadPageAsync(source, 1, default), "API response size is bounded");
        await CheckStoreAsync(Check);
        Console.WriteLine($"JSON API: {count} checks passed; mock HTTP and isolated SQLite, no AI calls or production database.");
    }

    private static string Page(int page, int last, params (int Id, string Slug)[] items) => JsonSerializer.Serialize(new
    { success = true, data = new { items = items.Select(x => new { id = x.Id, slug = x.Slug }).ToArray(), meta = new { current_page = page, last_page = last } } });
    private static string Detail(int id, string slug, string? body = null) => JsonSerializer.Serialize(new
    { success = true, data = new { job = new { id, slug, title = "Engineer", description = body ?? "<p>Full announcement with detailed responsibilities and qualifications. This is the actual full job description, longer than a summary.</p><script>SECRET_SCRIPT</script><a href='https://example.org/apply'>Apply here</a>", expires_at = "2030-12-01", original_url = "https://example.org/apply" } } });

    // Explicit opt-in: public source GETs only. Honors the environment's proxy configuration.
    public static async Task RunLiveAsync()
    {
        var source = SourceApiProfiles.ForWebsite("https://jobmatch.ps/jobs?country=1000000055&page=1&per_page=5")!;
        using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(40) };
        using var reader = new JsonApiReader(new UrlPolicy(), http);
        var first = await reader.ReadPageAsync(source, 1, default);
        if (first.Items.Count == 0) throw new Exception("No public jobs available to verify a detail sample.");
        var detail = await reader.ReadDetailAsync(source, first.Items[0].Url, default);
        var second = first.HasNext ? await reader.ReadPageAsync(source, 2, default) : default;
        Console.WriteLine($"Live Jobmatch API: first page {first.Items.Count} jobs; second page {second.Items?.Count ?? 0} jobs; full detail {detail.Text.Length} characters. No database or AI calls.");
    }

    private static async Task CheckStoreAsync(Action<bool,string> check)
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        connection.CreateCollation("Latin1_General_100_BIN2", string.CompareOrdinal);
        await using var db = new FixtureDb(new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection).Options);
        var tables = new[] { typeof(SourceItem), typeof(ScrapeRun), typeof(DiscoveryCheckpoint), typeof(AuditEntry) }
            .Select(t => db.Model.FindEntityType(t)!.GetTableName()).ToArray();
        foreach (var sql in db.Database.GenerateCreateScript().Split(';', StringSplitOptions.RemoveEmptyEntries))
            if (tables.Any(t => sql.TrimStart().StartsWith($"CREATE TABLE \"{t}\"")))
                await db.Database.ExecuteSqlRawAsync(sql);
        db.DiscoveryCheckpoints.Add(new() { SourceId = 1, NextUrl = "https://example.org/list" });
        var run = new ScrapeRun { SourceId = 1 };
        db.Runs.Add(run);
        db.SourceItems.Add(new() { SourceId = 1, ExternalId = "11", Url = "https://jobmatch.ps/jobs/old-slug", Status = ProcessingStatus.Published });
        await db.SaveChangesAsync();
        var batch = new DiscoveryBatch([("11", "https://jobmatch.ps/jobs/new-slug"), ("12", "https://jobmatch.ps/jobs/second")], null, 1);
        await new DiscoveryBatchStore(db).SaveAsync(1, ConnectorKind.JsonApi, run.Id, 1, DateTimeOffset.UtcNow.AddDays(-1), batch, default);
        check(await db.SourceItems.CountAsync() == 2 && (await db.SourceItems.SingleAsync(x => x.ExternalId == "11")).Url.EndsWith("/new-slug"),
            "Stable API ID updates the saved slug without creating a duplicate source item");
        check((await db.DiscoveryCheckpoints.SingleAsync()).Completed && run.Found == 2,
            "API batch and completed checkpoint are stored together in the existing transaction");
    }

    private sealed class FixtureDb(DbContextOptions<HiringDb> options) : HiringDb(options)
    {
        protected override void OnModelCreating(ModelBuilder model)
        {
            base.OnModelCreating(model);
            foreach (var entity in model.Model.GetEntityTypes()) foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?)) property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
                if (property.Name == "Version" && property.ClrType == typeof(byte[])) property.ValueGenerated = ValueGenerated.Never;
            }
        }
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        public string Mode { get; set; } = "normal";
        public bool FailSecondPage { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.AbsoluteUri; Urls.Add(url);
            if (request.Method != HttpMethod.Get || request.Headers.Contains("X-API-Key")) throw new Exception("Unexpected method/key");
            var body = url.EndsWith("/first") ? Detail(11, "first") : url.Contains("page=2") ? Page(2, 2, (12, "second")) : Page(1, 2, (11, "first"));
            var response = new HttpResponseMessage((FailSecondPage && url.Contains("page=2") ? "error" : Mode) switch { "error" => HttpStatusCode.InternalServerError, "redirect" => HttpStatusCode.Redirect, _ => HttpStatusCode.OK });
            response.Content = new StringContent(Mode switch { "html" => "<html>Blocked</html>", "large" => new string('x', 4 * 1024 * 1024 + 1), _ => body }, System.Text.Encoding.UTF8, Mode == "html" ? "text/html" : "application/json");
            response.Headers.Location = new Uri("https://unapproved.example/");
            return Task.FromResult(response);
        }
    }
}
