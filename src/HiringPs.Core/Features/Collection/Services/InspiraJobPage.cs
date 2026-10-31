using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>ينتظر وثيقة وظيفة PeopleSoft الفعلية، حتى لو بقي الغلاف في loading أو عُرضت التفاصيل داخل إطار.</summary>
public static class InspiraJobPage
{
    /// <summary>يميز تفاصيل إعلان من الصفحة العامة؛ HTTP 200 أو عنوان Applicant Home وحدهما لا يكفيان.</summary>
    public static bool HasJobDetails(string text) => text.Length >= 300
        && Regex.IsMatch(text, @"duty\s+station|موقع\s+العمل|مكان\s+العمل", RegexOptions.IgnoreCase)
        && Regex.IsMatch(text, @"responsibilities|org\.?\s+setting\s+and\s+reporting|المهام|الوصف\s+الوظيفي", RegexOptions.IgnoreCase)
        && Regex.IsMatch(text, @"\bjob\s+opening\b|posting\s+period|رقم\s+(?:الوظيفة|الإعلان)|تاريخ\s+النشر", RegexOptions.IgnoreCase);

    /// <summary>يفحص الصفحة وإطارات النطاق المعتمد بمهلة محدودة؛ لا يسجل دخولًا ولا يستنتج الإغلاق من عدم الوصول.</summary>
    public static async Task<string> ReadAsync(IPage page, IEnumerable<string> allowedHosts, TimeSpan timeout, CancellationToken ct)
    {
        var hosts = allowedHosts.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var started = System.Diagnostics.Stopwatch.StartNew();
        while (started.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var frame in page.Frames)
            {
                if (!Allowed(frame, hosts)) continue;
                try
                {
                    var remaining = timeout - started.Elapsed;
                    if (remaining <= TimeSpan.Zero) break;
                    var text = await frame.EvaluateAsync<string>("() => document.body?.innerText || ''")
                        .WaitAsync(remaining < TimeSpan.FromSeconds(2) ? remaining : TimeSpan.FromSeconds(2), ct);
                    if (HasJobDetails(text))
                        return await frame.ContentAsync().WaitAsync(timeout - started.Elapsed > TimeSpan.Zero
                            ? timeout - started.Elapsed : TimeSpan.FromMilliseconds(1), ct);
                }
                catch (Exception ex) when (ex is PlaywrightException or TimeoutException) { /* An iframe may navigate while the portal initializes. */ }
            }
            var delay = timeout - started.Elapsed;
            if (delay > TimeSpan.Zero) await Task.Delay(delay < TimeSpan.FromMilliseconds(250) ? delay : TimeSpan.FromMilliseconds(250), ct);
        }
        throw new ContentRetryableException("Inspira did not expose readable job details before the content timeout; the applicant-home shell is not the job announcement. Retry scheduled; no source closure inferred.");
    }

    /// <summary>لا يقرأ iframe أجنبيًا؛ وثائق about الفارغة/المضمنة ترث نطاق الإطار الأب المعتمد فقط.</summary>
    private static bool Allowed(IFrame frame, HashSet<string> hosts)
    {
        for (var current = frame; current is not null; current = current.ParentFrame)
        {
            if (current.Url is "about:blank" or "about:srcdoc" or "") continue;
            return Uri.TryCreate(current.Url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && hosts.Contains(uri.IdnHost);
        }
        return false;
    }
}
