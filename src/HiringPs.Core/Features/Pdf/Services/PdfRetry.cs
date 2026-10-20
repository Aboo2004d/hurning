using System.Text.Json;

namespace HiringPs.Core;

/// <summary>يحدد متى يلزم جلب ملف PDF من جديد بدل إعادة استخدام دليل قديم.</summary>
public static class PdfRetry
{
    /// <summary>يحدد ما إذا كانت إعادة معالجة PDF تحتاج إعادة جلب المحتوى الأصلي.</summary>
    public static bool NeedsFreshContent(SourceItem item)
    {
        if (
            Uri.TryCreate(item.Url, UriKind.Absolute, out var uri)
            && uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
        )
            return true;
        if (
            item.Content.Contains("[PDF PAGE", StringComparison.Ordinal)
            || item.Content.Contains("PDF ATTACHMENT", StringComparison.OrdinalIgnoreCase)
        )
            return true;
        try
        {
            return (JsonSerializer.Deserialize<string[]>(item.AttachmentsJson) ?? []).Length > 0;
        }
        catch (JsonException)
        {
            return true;
        } // Invalid attachment metadata should be fetched again.
    }
}
