using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>يتابع أحداث صفحة Playwright خلال نطاق محدد ثم يفصل الاشتراكات عند التخلص منه.</summary>
public sealed class CollectorDiagnosticSession : IDisposable
{
    private static readonly ConditionalWeakTable<IPage, CollectorDiagnosticSession> Sessions =
        new();
    private readonly IPage page;
    private readonly IConfiguration configuration;
    private readonly Stopwatch total = Stopwatch.StartNew();
    private readonly Stopwatch stage = Stopwatch.StartNew();
    private readonly CollectorDiagnostic data;

    public CollectorDiagnosticSession(
        IPage page,
        Source source,
        string operation,
        string url,
        IConfiguration configuration
    )
    {
        this.page = page;
        this.configuration = configuration;
        data = new()
        {
            SourceId = source.Id,
            Operation = operation,
            RequestedUrl = CollectorDiagnosticFiles.SafeUrl(url),
        };
        Sessions.Add(page, this);
        page.Response += Response;
        page.RequestFailed += Failed;
    }

    /// <summary>يسجل مرحلة تشخيصية محددة دون حمولة مزود أو مفاتيح سرية.</summary>
    public void Step(string name, string selector = "", int timeoutMs = 0)
    {
        data.Stage = name;
        data.Selector = selector.Length > 300 ? selector[..300] : selector;
        data.TimeoutMs = timeoutMs;
        stage.Restart();
    }

    /// <summary>ينفذ عملية الخدمة ضمن حدود الإلغاء والموارد مع الحفاظ على فشلها الفعلي.</summary>
    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        try
        {
            return await work();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            page.Response -= Response;
            page.RequestFailed -= Failed;
            Sessions.Remove(page);
            data.ElapsedMs = total.ElapsedMilliseconds;
            data.StageElapsedMs = stage.ElapsedMilliseconds;
            data.ExceptionType = ex.GetType().Name;
            data.FinalUrl = CollectorDiagnosticFiles.SafeUrl(page.Url);
            try
            {
                data.Title = await page.TitleAsync().WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch { }

            if (data.Title.Length > 200)
                data.Title = data.Title[..200];
            try
            {
                data.ReadyState = await page.EvaluateAsync<string>("() => document.readyState")
                    .WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch { }

            if (data.Selector.Length > 0)
            {
                try
                {
                    data.SelectorMatches = await page.Locator(data.Selector)
                        .CountAsync()
                        .WaitAsync(TimeSpan.FromSeconds(2));
                    data.SelectorVisible = await page.Locator(data.Selector)
                        .First.IsVisibleAsync()
                        .WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch { }
            }

            try
            {
                Directory.CreateDirectory(CollectorDiagnosticFiles.DirectoryPath(configuration));
                // Bounded screenshot count/size; form input values are masked and no HTML/cookies/headers are saved.
                if (
                    Directory
                        .EnumerateFiles(
                            CollectorDiagnosticFiles.DirectoryPath(configuration),
                            "*.jpg"
                        )
                        .Take(500)
                        .Count() < 500
                )
                {
                    try
                    {
                        var bytes = await page.ScreenshotAsync(
                            new()
                            {
                                Type = ScreenshotType.Jpeg,
                                Quality = 45,
                                FullPage = false,
                                Timeout = 2000,
                                Mask = [page.Locator("input,textarea,[contenteditable=true]")],
                            }
                        );
                        if (bytes.Length <= 2 * 1024 * 1024)
                        {
                            await File.WriteAllBytesAsync(
                                CollectorDiagnosticFiles.FilePath(configuration, data.Id, true),
                                bytes
                            );
                            data.Screenshot = true;
                        }
                    }
                    catch
                    {
                        data.CaptureNote = "Screenshot unavailable; report retained.";
                    }
                }

                await File.WriteAllTextAsync(
                    CollectorDiagnosticFiles.FilePath(configuration, data.Id),
                    JsonSerializer.Serialize(data, CollectorDiagnosticFiles.Json)
                );
            }
            catch
            {
                data.CaptureNote = "Capture storage unavailable; metadata only.";
            }

            throw new CollectorDiagnosticException(data, ex);
        }
    }

    /// <summary>يسجل حقيقة تشخيصية محدودة عن استجابة المتصفح دون حفظ أسرار.</summary>
    private void Response(object? sender, IResponse response)
    {
        if (response.Request.IsNavigationRequest && response.Frame == page.MainFrame)
            data.HttpStatus = response.Status;
        if (response.Status >= 400)
            Add(response.Url, response.Request.ResourceType, "HttpFailure", response.Status);
    }

    /// <summary>يسجل فشل طلب متصفح ضمن جلسة التشخيص.</summary>
    private void Failed(object? sender, IRequest request)
    {
        if (request.ResourceType is "image" or "font" or "media")
            return;
        var failure = request.Failure ?? "NetworkFailure";
        // Keep only the browser error code, never arbitrary exception/body text.
        var code = System.Text.RegularExpressions.Regex.Match(failure, @"net::ERR_[A-Z0-9_]+");
        Add(request.Url, request.ResourceType, code.Success ? code.Value : "NetworkFailure", null);
    }

    /// <summary>يضيف حدثًا مسموحًا إلى تشخيص الجمع.</summary>
    private void Add(string url, string type, string reason, int? status)
    {
        if (data.Network.Count < 30)
            data.Network.Add(new(CollectorDiagnosticFiles.SafeUrl(url), type, reason, status));
    }

    /// <summary>يسجل منع عنوان بموجب سياسة الوصول بدل تجاوزه.</summary>
    public static void PolicyBlock(IRequest request)
    {
        try
        {
            if (Sessions.TryGetValue(request.Frame.Page, out var session))
                session.Add(request.Url, request.ResourceType, "PolicyBlocked", null);
        }
        catch { }
    }

    /// <summary>يسجل المرحلة الحالية من تنقل صفحة المصدر.</summary>
    public static void PageStep(IPage page, string stage, int timeoutMs)
    {
        if (Sessions.TryGetValue(page, out var session))
            session.Step(stage, timeoutMs: timeoutMs);
    }

    /// <summary>يفصل تتبع أحداث المتصفح ويحرر موارد التشخيص.</summary>
    public void Dispose()
    {
        page.Response -= Response;
        page.RequestFailed -= Failed;
        Sessions.Remove(page);
    }
}
