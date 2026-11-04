using System.Net;
using System.Text;
using System.Text.Json;
using HiringPs.Core;
using Microsoft.Extensions.Configuration;

public static class JsonApiPostChecks
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        void Reject(Action work, string name)
        {
            try { work(); } catch (Exception ex) when (ex is InvalidOperationException or ContentNeedsReviewException or JsonException)
            { Check(true, name); return; }
            throw new Exception("Accepted: " + name);
        }
        var source = SourceApiProfiles.ForWebsite("https://foras.ps/")!;
        source.Enabled = source.UsageApproved = true;
        var conf = JsonApiReader.Configuration(source);
        var api = conf.Api!;
        Check(source.Connector == ConnectorKind.JsonApi && source.Url == "https://foras.ps/opportunities",
            "Foras uses the existing shared connector with a canonical public listing URL");
        using (var body = JsonDocument.Parse(JsonApiReader.PageBody(api, 1)))
            Check(!body.RootElement.TryGetProperty("categories", out _) && body.RootElement.GetProperty("pageNumber").GetInt32() == 1,
                "All opportunity categories are retained and pagination begins at the tested page one");
        Check(SourceApiProfiles.ForWebsite("https://foras.ps/opportunities") is not null
            && SourceApiProfiles.ForWebsite("https://foras.ps.evil.test/") is null,
            "Foras preset is restricted to the observed source host and routes");
        Reject(() => SourceApiProfiles.ForWebsite("https://foras.ps/opportunities?category=3"), "Unverified filters cannot silently turn into an all-category source");
        Check(JsonApiReader.PageCursor(api, 1) != JsonApiReader.PageCursor(api, 2)
            && JsonApiReader.PageUrl(api, 1) == JsonApiReader.PageUrl(api, 2),
            "POST search pages have distinct checkpoint cursors while retaining the API endpoint");
        api.PageSize = 2;
        api.ListingBodyTemplate = """{"pageNumber":{page},"pageSize":2,"orderBy":"date","orderByDirection":"Desc"}""";
        source.ConfigurationJson = JsonSerializer.Serialize(conf);
        using (var json = JsonDocument.Parse(Page(3, "id-1", "id-2")))
            Check(JsonApiReader.ParsePage(json.RootElement, api, 1) is { HasNext: true, Items.Count: 2 }, "Total-count pagination continues after a complete nonfinal page");
        using (var json = JsonDocument.Parse(Page(3, "id-3")))
            Check(JsonApiReader.ParsePage(json.RootElement, api, 2) is { HasNext: false, Items.Count: 1 }, "Partial last page completes only when its size agrees with total metadata");
        using (var json = JsonDocument.Parse(Page(0)))
            Check(JsonApiReader.ParsePage(json.RootElement, api, 1) is { HasNext: false, Items.Count: 0 }, "An explicitly empty total-count listing is valid");
        foreach (var payload in new[] { Page(3, "id-1"), Page(-1), "{\"result\":[]}" })
        {
            using var json = JsonDocument.Parse(payload);
            Reject(() => JsonApiReader.ParsePage(json.RootElement, api, 1), "Incomplete or invalid total-count response cannot complete discovery");
        }
        using (var json = JsonDocument.Parse(Detail("id-1")))
        {
            var bundle = JsonApiReader.ParseDetail(json.RootElement, api, "https://foras.ps/opportunities/job-details/id-1", "id-1");
            Check(bundle.Text.Contains("Title (Arabic): عنوان عربي") && bundle.Text.Contains("Description (English): English detail")
                && bundle.Text.Contains("https://example.org/apply") && !bundle.Text.Contains("SECRET_SCRIPT"),
                "Root detail and translated arrays retain both languages and application evidence independently of array order");
        }
        using (var json = JsonDocument.Parse(Detail("id-1", arabic: false)))
            Check(JsonApiReader.ParseDetail(json.RootElement, api, "https://foras.ps/opportunities/job-details/id-1", "id-1").Text.Contains("English detail"),
                "Full English content is accepted when an Arabic translation is absent");
        using (var json = JsonDocument.Parse("""{"info":[{"language":"ar","name":"one"},{"language":"ar","name":"two"}]}"""))
            Check(JsonApiReader.At(json.RootElement, "info[language=ar].name").ValueKind == JsonValueKind.Undefined,
                "Ambiguous language matches do not select an arbitrary row");
        var copy = JsonSerializer.Deserialize<SourceConfiguration>(source.ConfigurationJson)!;
        copy.Api!.ListingMethod = "DELETE";
        Reject(() => JsonApiReader.Validate(source.Url, copy), "Shared reader refuses methods other than GET and public POST search");
        copy.Api.ListingMethod = "POST"; copy.Api.ListingBodyTemplate = "[]";
        Reject(() => JsonApiReader.Validate(source.Url, copy), "POST search configuration requires a paginated JSON object");
        copy.Api.ListingBodyTemplate = api.ListingBodyTemplate; copy.Api.Fields[0] = new("Bad", "info[language=ar OR en].name");
        Reject(() => JsonApiReader.Validate(source.Url, copy), "Array paths permit exact equality selectors rather than executable query expressions");

        using var handler = new FixtureHandler();
        using var http = new HttpClient(handler);
        using var reader = new JsonApiReader(new UrlPolicy(), http);
        var config = new ConfigurationBuilder().Build();
        await using var browser = new BrowserCollector(new UrlPolicy(), config, new AttachmentReader(new UrlPolicy(), config), apiReader: reader);
        var checkpoint = new DiscoveryCheckpoint { NextUrl = source.Url };
        var batches = new List<DiscoveryBatch>();
        await browser.DiscoverResumableAsync(source, checkpoint, (batch, _) =>
        {
            batches.Add(batch); checkpoint.CompletedPages = batch.CompletedPages;
            checkpoint.NextUrl = batch.NextUrl ?? ""; checkpoint.Completed = batch.NextUrl is null;
            return Task.CompletedTask;
        }, default);
        Check(handler.Pages.SequenceEqual(new[] { 1, 2 }) && batches.Count == 2 && checkpoint.Completed
            && batches[0].NextUrl == JsonApiReader.PageCursor(api, 2),
            "Production discovery sends POST search bodies and saves one checkpoint per page");
        handler.Pages.Clear();
        checkpoint = new() { CompletedPages = 1, NextUrl = JsonApiReader.PageCursor(api, 2) };
        await browser.DiscoverResumableAsync(source, checkpoint, (_, _) => Task.CompletedTask, default);
        Check(handler.Pages.SequenceEqual(new[] { 2 }), "POST discovery restart resumes from the next saved page body");
        var result = await new SourceSetupAssistant(http, browser, config).SuggestAsync("https://foras.ps/", "ar", default);
        Check(result.Connector == 8 && result.Preview.SampleText.Contains("عنوان عربي") && result.Url == source.Url,
            "Setup assistant previews Foras with the same reader and no configured AI key");
        Console.WriteLine($"POST JSON API: {count} checks passed; mock HTTP, no production database or AI calls.");
    }

    public static async Task RunLiveAsync()
    {
        var source = SourceApiProfiles.ForWebsite("https://foras.ps/")!;
        using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false });
        using var reader = new JsonApiReader(new UrlPolicy(), http);
        var first = await reader.ReadPageAsync(source, 1, default);
        var second = await reader.ReadPageAsync(source, 2, default);
        if (first.Items.Count == 0 || second.Items.Count == 0 || first.Items.Select(x => x.Id).SequenceEqual(second.Items.Select(x => x.Id)))
            throw new Exception("Live Foras pagination did not expose distinct populated pages.");
        var detail = await reader.ReadDetailAsync(source, first.Items[0].Url, default);
        Console.WriteLine($"Live Foras shared API: page 1 {first.Items.Count} items, page 2 {second.Items.Count} items; full detail {detail.Text.Length} characters; all categories, no database or AI calls.");
    }

    private static string Page(int total, params string[] ids) => JsonSerializer.Serialize(new { totalRecords = total, result = ids.Select(id => new { id }).ToArray() });
    private static string Detail(string id, bool arabic = true)
    {
        var info = new List<object> { new { language = "en", name = "English title", description = "<p>English detail with full eligibility requirements and application instructions. This is a complete public opportunity description.</p>" } };
        if (arabic) info.Add(new { language = "ar", name = "عنوان عربي", description = "<p>وصف عربي كامل مع تفاصيل الفرصة وشروطها وطريقة التقديم. هذا المحتوى يتضمن المعلومات الكاملة المطلوبة للإعلان الأصلي.</p><script>SECRET_SCRIPT</script>" });
        return JsonSerializer.Serialize(new { id, info, applyUrl = "https://example.org/apply", endDate = "2030-12-01" });
    }
    private sealed class FixtureHandler : HttpMessageHandler
    {
        public List<int> Pages { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.Fragment.Length > 0) throw new Exception("Checkpoint marker must never be sent to API");
            string body;
            if (request.Method == HttpMethod.Post)
            {
                if (request.RequestUri.AbsoluteUri != "https://foras.ps/api/v1/Opportunities/filteredJobs") throw new Exception("Unexpected endpoint");
                using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                var page = payload.RootElement.GetProperty("pageNumber").GetInt32(); Pages.Add(page);
                if (payload.RootElement.TryGetProperty("categories", out _)) throw new Exception("Unexpected category restriction");
                var size = payload.RootElement.GetProperty("pageSize").GetInt32();
                body = size == 10 ? Page(1, "id-1") : page == 1 ? Page(3, "id-1", "id-2") : Page(3, "id-3");
            }
            else
            {
                if (request.Method != HttpMethod.Get) throw new Exception("Unexpected detail method");
                body = Detail(request.RequestUri.Segments.Last());
            }
            return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
