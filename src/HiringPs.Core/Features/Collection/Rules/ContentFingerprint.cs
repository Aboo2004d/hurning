using System.Text.RegularExpressions;

namespace HiringPs.Core;

/// <summary>بصمة النص الأساسي مع توافق البصمات القديمة؛ تتجاهل قوائم الروابط المتغيرة.</summary>
public static class ContentFingerprint
{
    public const string LinksMarker = "SOURCE LINKS:";

    /// <summary>يستبعد قسم روابط المصدر ثم يوحد الفراغات قبل المقارنة.</summary>
    public static string MainText(string content)
    {
        var links = content.IndexOf(LinksMarker, StringComparison.Ordinal);
        var main = links >= 0 ? content[..links] : content;
        return Regex.Replace(main.Normalize(), @"\s+", " ").Trim();
    }

    /// <summary>ينشئ بصمة محتوى مستقرة لا تتغير بتبديل ترتيب روابط الصفحة.</summary>
    public static string Hash(string content) => TextRules.Hash(MainText(content));

    // Hashes stored before this rule covered the complete text; accept either for reuse.
    /// <summary>يقبل البصمة الحالية أو البصمة القديمة للنص الكامل لإعادة استخدام الاستخراج.</summary>
    public static bool Matches(string storedHash, string content) =>
        storedHash.Length > 0
        && (storedHash == Hash(content) || storedHash == TextRules.Hash(content));
}
