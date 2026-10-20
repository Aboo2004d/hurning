using System.Text.RegularExpressions;

namespace HiringPs.Core;

/// <summary>يربط بيانات الإعلان بأدلتها ويعزل النصوص المرتبطة التي لا تصف الإعلان الأساسي.</summary>
public static class SourceEvidence
{
    /// <summary>يجمع أدلة العنصر التي تستخدم للتحقق من نتائج الاستخراج.</summary>
    public static string ForItem(SourceItem item) =>
        Prepare(item.Source.Connector, item.Url, item.Content);

    /// <summary>يجهز ملفات العمل أو الأدلة اللازمة مع التحقق من حدود استخدامها.</summary>
    public static string Prepare(ConnectorKind connector, string originalUrl, string content)
    {
        if (connector != ConnectorKind.PalCareer)
            return content;
        var links = content.IndexOf("SOURCE LINKS:", StringComparison.Ordinal);
        var text = links >= 0 ? content[..links] : content;
        var related = text.IndexOf("وظائف مشابهة", StringComparison.Ordinal);
        if (related >= 0)
            text = text[..related];
        return text.Trim() + "\nSOURCE LINKS:\n" + originalUrl;
    }

    /// <summary>يستخرج عنوان الإعلان الأساسي من مصدر PalCareer.</summary>
    public static string PrimaryPalCareerTitle(string content)
    {
        var match = Regex.Match(content, @"اتصل بنا\s+(?<title>.{2,250}?)\s+\k<title>(?:\s|$)");
        return match.Success ? match.Groups["title"].Value.Trim() : "";
    }

    /// <summary>يحدد ما إذا كان دليل التاريخ يخص النشر بدل موعد التقديم.</summary>
    public static bool IsPublicationDate(string evidence) =>
        Regex.IsMatch(
            evidence,
            @"تاريخ\s+النشر|publication\s+date|posted\s+(?:on|date)",
            RegexOptions.IgnoreCase
        );

    /// <summary>يكشف النص الذي يصف محتوى مرتبطًا وليس الإعلان الأساسي.</summary>
    public static bool IsRelatedOnly(SourceItem item, string title)
    {
        if (
            item.Source.Connector != ConnectorKind.PalCareer
            || TextEvidence.TitleContains(ForItem(item), title)
        )
            return false;
        var start = item.Content.IndexOf("وظائف مشابهة", StringComparison.Ordinal);
        if (start < 0)
            return false;
        var end = item.Content.IndexOf("SOURCE LINKS:", start, StringComparison.Ordinal);
        var related = end >= 0 ? item.Content[start..end] : item.Content[start..];
        return TextEvidence.TitleContains(related, title);
    }

    /// <summary>يختار سبب المراجعة المرتبط بالأدلة الحالية.</summary>
    public static string RelevantReviewReason(string reason)
    {
        // Remove only obsolete deterministic checks; other AI concerns still require review.
        var text = Regex.Replace(
            reason,
            @"Summary must contain 1[–-]200 characters\.?|Invalid application link\.?|Apply URL not in source text; use original source\.?|AI field \w+ exceeds schema limit; inspect stored source\.?",
            "",
            RegexOptions.IgnoreCase
        );
        text = text.Replace(
                "Deadline is unspecified; verify original source before publication.",
                "",
                StringComparison.OrdinalIgnoreCase
            )
            .Replace(
                "موعد انتهاء الإعلان غير محدد؛ بانتظار المراجعة. افتح الإعلان الأصلي للتحقق قبل الاعتماد.",
                "",
                StringComparison.Ordinal
            );
        return text.Trim();
    }
}
