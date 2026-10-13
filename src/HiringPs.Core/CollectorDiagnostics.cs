using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

public record NetworkDiagnostic(string Url, string Type, string Reason, int? Status);
public class CollectorDiagnostic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SourceId { get; set; }
    public string Operation { get; set; } = "";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
    public string Stage { get; set; } = "";
    public string Selector { get; set; } = "";
    public int TimeoutMs { get; set; }
    public long ElapsedMs { get; set; }
    public long StageElapsedMs { get; set; }
    public string RequestedUrl { get; set; } = "";
    public string FinalUrl { get; set; } = "";
    public string Title { get; set; } = "";
    public int? HttpStatus { get; set; }
    public string ReadyState { get; set; } = "";
    public int? SelectorMatches { get; set; }
    public bool? SelectorVisible { get; set; }
    public string ExceptionType { get; set; } = "";
    public bool Screenshot { get; set; }
    public string CaptureNote { get; set; } = "";
    public List<NetworkDiagnostic> Network { get; set; } = [];
}
public class CollectorDiagnosticException(CollectorDiagnostic diagnostic, Exception original) : Exception(original.Message, original)
{
    public CollectorDiagnostic Diagnostic { get; } = diagnostic;
}
public static class CollectorDiagnosticFiles
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static string DirectoryPath(IConfiguration configuration)
    {
        if (configuration["Diagnostics:Directory"] is { Length: > 0 } configured)
        {
            if (!Path.IsPathFullyQualified(configured)) throw new InvalidOperationException("Diagnostics directory must be absolute.");
            return Path.GetFullPath(configured);
        }
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "HiringPs.sln"))) return Path.Combine(dir.FullName, "artifacts", "collector-diagnostics");
        return Path.Combine(AppContext.BaseDirectory, "Data", "CollectorDiagnostics");
    }
    public static string FilePath(IConfiguration configuration, Guid id, bool image = false) => Path.Combine(DirectoryPath(configuration), id.ToString("N") + (image ? ".jpg" : ".json"));
    public static string AuditDetail(CollectorDiagnostic data, long? itemId = null) => JsonSerializer.Serialize(new
    {
        id = data.Id.ToString("N"), data.SourceId, itemId, data.Operation, data.At, data.Stage, data.HttpStatus,
        data.ElapsedMs, data.StageElapsedMs, data.TimeoutMs,
        selector = data.Selector.Length > 150 ? data.Selector[..150] : data.Selector,
        finalUrl = data.FinalUrl.Length > 300 ? data.FinalUrl[..300] : data.FinalUrl,
        data.ExceptionType, data.Screenshot, data.CaptureNote
    });
    public static async Task<T> WithoutPageAsync<T>(Source source, string operation, string stage, string url, IConfiguration configuration, Func<Task<T>> work, CancellationToken ct)
    {
        var started = Stopwatch.StartNew();
        try { return await work(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var report = new CollectorDiagnostic { SourceId=source.Id, Operation=operation, Stage=stage, RequestedUrl=SafeUrl(url), FinalUrl=SafeUrl(url), ElapsedMs=started.ElapsedMilliseconds, StageElapsedMs=started.ElapsedMilliseconds, ExceptionType=ex.GetType().Name, HttpStatus=ex is HttpRequestException http && http.StatusCode is {} code?(int)code:null };
            try { Directory.CreateDirectory(DirectoryPath(configuration)); await File.WriteAllTextAsync(FilePath(configuration,report.Id),JsonSerializer.Serialize(report,Json)); } catch { report.CaptureNote="Capture storage unavailable; metadata only."; }
            throw new CollectorDiagnosticException(report,ex);
        }
    }
    public static string SafeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return "";
        var safeKeys = new[] { "id", "JobOpeningId", "PostingSeq", "page", "language", "languageCd", "vacancyNum" };
        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(part =>
        {
            var bits = part.Split('=', 2); var key = Uri.UnescapeDataString(bits[0]);
            return safeKeys.Contains(key, StringComparer.OrdinalIgnoreCase) && bits.Length == 2 && bits[1].Length <= 80 ? Uri.EscapeDataString(key) + "=" + bits[1] : "redacted=1";
        });
        return new UriBuilder(uri) { UserName = "", Password = "", Fragment = "", Query = string.Join('&', query) }.Uri.ToString();
    }
}

public sealed class CollectorDiagnosticSession : IDisposable
{
    private static readonly ConditionalWeakTable<IPage, CollectorDiagnosticSession> Sessions = new();
    private readonly IPage page;
    private readonly IConfiguration configuration;
    private readonly Stopwatch total = Stopwatch.StartNew();
    private readonly Stopwatch stage = Stopwatch.StartNew();
    private readonly CollectorDiagnostic data;
    public CollectorDiagnosticSession(IPage page, Source source, string operation, string url, IConfiguration configuration)
    {
        this.page = page; this.configuration = configuration;
        data = new() { SourceId = source.Id, Operation = operation, RequestedUrl = CollectorDiagnosticFiles.SafeUrl(url) };
        Sessions.Add(page, this); page.Response += Response; page.RequestFailed += Failed;
    }
    public void Step(string name, string selector = "", int timeoutMs = 0)
    {
        data.Stage = name; data.Selector = selector.Length > 300 ? selector[..300] : selector; data.TimeoutMs = timeoutMs; stage.Restart();
    }
    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        try { return await work(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            page.Response -= Response; page.RequestFailed -= Failed; Sessions.Remove(page);
            data.ElapsedMs = total.ElapsedMilliseconds; data.StageElapsedMs = stage.ElapsedMilliseconds;
            data.ExceptionType = ex.GetType().Name; data.FinalUrl = CollectorDiagnosticFiles.SafeUrl(page.Url);
            try { data.Title = await page.TitleAsync().WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
            if (data.Title.Length > 200) data.Title = data.Title[..200];
            try { data.ReadyState = await page.EvaluateAsync<string>("() => document.readyState").WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
            if (data.Selector.Length > 0)
            {
                try { data.SelectorMatches = await page.Locator(data.Selector).CountAsync().WaitAsync(TimeSpan.FromSeconds(2)); data.SelectorVisible = await page.Locator(data.Selector).First.IsVisibleAsync().WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
            }
            try
            {
                Directory.CreateDirectory(CollectorDiagnosticFiles.DirectoryPath(configuration));
                // Bounded screenshot count/size; form input values are masked and no HTML/cookies/headers are saved.
                if (Directory.EnumerateFiles(CollectorDiagnosticFiles.DirectoryPath(configuration), "*.jpg").Take(500).Count() < 500)
                {
                    try
                    {
                        var bytes = await page.ScreenshotAsync(new() { Type = ScreenshotType.Jpeg, Quality = 45, FullPage = false, Timeout = 2000, Mask = [page.Locator("input,textarea,[contenteditable=true]")] });
                        if (bytes.Length <= 2 * 1024 * 1024) { await File.WriteAllBytesAsync(CollectorDiagnosticFiles.FilePath(configuration, data.Id, true), bytes); data.Screenshot = true; }
                    }
                    catch { data.CaptureNote = "Screenshot unavailable; report retained."; }
                }
                await File.WriteAllTextAsync(CollectorDiagnosticFiles.FilePath(configuration, data.Id), JsonSerializer.Serialize(data, CollectorDiagnosticFiles.Json));
            }
            catch { data.CaptureNote = "Capture storage unavailable; metadata only."; }
            throw new CollectorDiagnosticException(data, ex);
        }
    }
    private void Response(object? sender, IResponse response)
    {
        if (response.Request.IsNavigationRequest && response.Frame == page.MainFrame) data.HttpStatus = response.Status;
        if (response.Status >= 400) Add(response.Url, response.Request.ResourceType, "HttpFailure", response.Status);
    }
    private void Failed(object? sender, IRequest request)
    {
        if (request.ResourceType is "image" or "font" or "media") return;
        var failure = request.Failure ?? "NetworkFailure";
        // Keep only the browser error code, never arbitrary exception/body text.
        var code = System.Text.RegularExpressions.Regex.Match(failure, @"net::ERR_[A-Z0-9_]+");
        Add(request.Url, request.ResourceType, code.Success ? code.Value : "NetworkFailure", null);
    }
    private void Add(string url, string type, string reason, int? status)
    {
        if (data.Network.Count < 30) data.Network.Add(new(CollectorDiagnosticFiles.SafeUrl(url), type, reason, status));
    }
    public static void PolicyBlock(IRequest request)
    {
        try { if (Sessions.TryGetValue(request.Frame.Page, out var session)) session.Add(request.Url, request.ResourceType, "PolicyBlocked", null); } catch { }
    }
    public static void PageStep(IPage page, string stage, int timeoutMs)
    {
        if (Sessions.TryGetValue(page, out var session)) session.Step(stage, timeoutMs: timeoutMs);
    }
    public void Dispose() { page.Response -= Response; page.RequestFailed -= Failed; Sessions.Remove(page); }
}
