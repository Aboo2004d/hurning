using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>المتصفح الذي أُطلق فعليًا وطريقة اختياره؛ لا تعني النتيجة نجاح المتصفحين معًا.</summary>
public sealed record BrowserLaunchResultDto(IBrowser Browser, string Name, bool UsedFallback)
{
    /// <summary>يعرض المتصفح والإصدار وسبب الانتقال إلى البديل عند حدوثه.</summary>
    public string Display => $"{Name}; Version {Browser.Version.Truncate(80)}"
        + (UsedFallback ? "; Edge launch failed; Chromium fallback succeeded" : "");
}
