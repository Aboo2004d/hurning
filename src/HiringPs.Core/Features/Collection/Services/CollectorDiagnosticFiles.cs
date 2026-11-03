using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>يحفظ تشخيص المتصفح بحدود الحجم والمسار دون كشف بيانات طلب حساسة.</summary>
public static class CollectorDiagnosticFiles
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <summary>يحسب مسار تخزين ملفات هذا المجال وفق الإعدادات وقاعدة المسارات الحالية.</summary>
    public static string DirectoryPath(IConfiguration configuration)
    {
        if (configuration["Diagnostics:Directory"] is { Length: > 0 } configured)
        {
            if (!Path.IsPathFullyQualified(configured))
                throw new InvalidOperationException("Diagnostics directory must be absolute.");
            return Path.GetFullPath(configured);
        }

        for (
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            dir is not null;
            dir = dir.Parent
        )
            if (File.Exists(Path.Combine(dir.FullName, "HiringPs.sln")))
                return Path.Combine(dir.FullName, "artifacts", "collector-diagnostics");
        return Path.Combine(AppContext.BaseDirectory, "Data", "CollectorDiagnostics");
    }

    /// <summary>يحسب مسار ملف التشخيص داخل المجلد المضبوط فقط.</summary>
    public static string FilePath(IConfiguration configuration, Guid id, bool image = false) =>
        Path.Combine(DirectoryPath(configuration), id.ToString("N") + (image ? ".jpg" : ".json"));

    /// <summary>ينشئ تفاصيل تدقيق محدودة تربط العملية بملف التشخيص دون محتواه.</summary>
    public static string AuditDetail(CollectorDiagnostic data, long? itemId = null)
    {
        var fields = new Dictionary<string, object?>
        {
            ["id"] = data.Id.ToString("N"), ["SourceId"] = data.SourceId, ["itemId"] = itemId,
            ["Operation"] = data.Operation.Truncate(60), ["At"] = data.At, ["Stage"] = data.Stage.Truncate(60),
            ["HttpStatus"] = data.HttpStatus, ["ElapsedMs"] = data.ElapsedMs, ["StageElapsedMs"] = data.StageElapsedMs,
            ["TimeoutMs"] = data.TimeoutMs, ["selector"] = data.Selector.Truncate(150),
            ["finalUrl"] = SafeUrl(data.FinalUrl).Truncate(300), ["requestedUrl"] = SafeUrl(data.RequestedUrl).Truncate(250),
            ["ExceptionType"] = data.ExceptionType.Truncate(100), ["Screenshot"] = data.Screenshot,
            ["CaptureNote"] = data.CaptureNote.Truncate(200), ["Title"] = data.Title.Truncate(80),
            ["ReadyState"] = data.ReadyState.Truncate(20), ["SelectorMatches"] = data.SelectorMatches,
            ["SelectorVisible"] = data.SelectorVisible,
            ["Network"] = data.Network.Take(3).Select(x => new NetworkDiagnostic(SafeUrl(x.Url).Truncate(120), x.Type.Truncate(20), x.Reason.Truncate(60), x.Status)).ToArray(),
        };
        var json = JsonSerializer.Serialize(fields);
        if (json.Length > 2000) { fields.Remove("Network"); json = JsonSerializer.Serialize(fields); }
        // AuditEntry.Detail is limited to 2000 characters. Keep valid JSON, never truncate the serialized document.
        while (json.Length > 2000)
        {
            var key = fields.Where(x => x.Key != "id" && x.Value is string value && value.Length > 20)
                .OrderByDescending(x => ((string)x.Value!).Length).First().Key;
            fields[key] = ((string)fields[key]!)[..(((string)fields[key]!).Length / 2)];
            json = JsonSerializer.Serialize(fields);
        }
        return json;
    }

    /// <summary>يسجل فشل الجمع عندما لم تُنشأ صفحة متصفح بعد.</summary>
    public static async Task<T> WithoutPageAsync<T>(
        Source source,
        string operation,
        string stage,
        string url,
        IConfiguration configuration,
        Func<Task<T>> work,
        CancellationToken ct
    )
    {
        var started = Stopwatch.StartNew();
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
            var report = new CollectorDiagnostic
            {
                SourceId = source.Id,
                Operation = operation,
                Stage = stage,
                RequestedUrl = SafeUrl(url),
                FinalUrl = SafeUrl(url),
                ElapsedMs = started.ElapsedMilliseconds,
                StageElapsedMs = started.ElapsedMilliseconds,
                ExceptionType = ex.GetType().Name,
                HttpStatus =
                    ex is HttpRequestException http && http.StatusCode is { } code
                        ? (int)code
                        : null,
            };
            try
            {
                Directory.CreateDirectory(DirectoryPath(configuration));
                await File.WriteAllTextAsync(
                    FilePath(configuration, report.Id),
                    JsonSerializer.Serialize(report, Json)
                );
            }
            catch (Exception storageError)
            {
                report.CaptureNote = "Capture storage unavailable (" + storageError.GetType().Name + "); metadata retained in audit.";
            }

            throw new CollectorDiagnosticException(report, ex);
        }
    }

    /// <summary>يحذف أجزاء العنوان غير المسموح بعرضها في التشخيص.</summary>
    public static string SafeUrl(string url)
    {
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http")
        )
            return "";
        var safeKeys = new[]
        {
            "id",
            "JobOpeningId",
            "PostingSeq",
            "page",
            "language",
            "languageCd",
            "vacancyNum",
        };
        var query = uri
            .Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var bits = part.Split('=', 2);
                var key = Uri.UnescapeDataString(bits[0]);
                return
                    safeKeys.Contains(key, StringComparer.OrdinalIgnoreCase)
                    && bits.Length == 2
                    && bits[1].Length <= 80
                    ? Uri.EscapeDataString(key) + "=" + bits[1]
                    : "redacted=1";
            });
        return new UriBuilder(uri)
        {
            UserName = "",
            Password = "",
            Fragment = "",
            Query = string.Join('&', query),
        }.Uri.ToString();
    }
}
