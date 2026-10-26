using Microsoft.Playwright;

namespace HiringPs.Core;

/// <summary>يحتفظ بمرحلة الفشل وسببه الأصلي ليستخدمهما الفحص والجمع الفعلي.</summary>
public sealed class BrowserRuntimeException(BrowserFailureStage stage, string detail, Exception inner)
    : PlaywrightException($"Stage={stage}; {detail}", inner)
{
    public BrowserFailureStage Stage { get; } = stage;
}
