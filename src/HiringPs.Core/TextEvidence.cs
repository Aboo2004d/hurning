using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace HiringPs.Core;

public static class TextEvidence
{
    // Formatting differences only. Never accept a quotation merely because it is semantically similar.
    public static string Normalize(string value)
    {
        var text = WebUtility.HtmlDecode(value).Normalize(NormalizationForm.FormKC)
            .Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace("ـ", "");
        text = Regex.Replace(text, "[\\u064B-\\u065F\\u0670\\u200B-\\u200F\\u202A-\\u202E\\uFEFF]", "");
        text = text.Replace('–', '-').Replace('—', '-').Replace('’', '\'');
        text = Regex.Replace(text, @"\s*/\s*", "/");
        text = Regex.Replace(text, @"(?<!\S)و\s+(?=ال)", "و");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
    public static bool Contains(string source, string quotation) => !string.IsNullOrWhiteSpace(quotation) && Normalize(source).Contains(Normalize(quotation), StringComparison.OrdinalIgnoreCase);

    public static bool TitleContains(string source, string title)
    {
        if (Contains(source, title)) return true;
        var parts = Regex.Split(title, @"\s+[-–—]\s+|[(),،]").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        return parts.Length > 1 && parts.All(x => Contains(source, x.Trim()));
    }

    public static bool OrganizationContains(string source, string organization)
    {
        if (Contains(source, organization)) return true;
        var normalizedSource = Normalize(source);
        var parts = Regex.Split(Normalize(organization), @"\s*/\s*|\s*[-–—]\s*|(?<=[\u0600-\u06FF])\s+(?=[A-Za-z])|(?<=[A-Za-z])\s+(?=[\u0600-\u06FF])").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        return parts.Length > 0 && parts.All(part =>
        {
            if (normalizedSource.Contains(part, StringComparison.OrdinalIgnoreCase)) return true;
            var name = Regex.Replace(part, @"^(?:شركة|مؤسسة|جمعية|Company)\s+", "", RegexOptions.IgnoreCase);
            return name != part && name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2
                && normalizedSource.Contains(name, StringComparison.OrdinalIgnoreCase);
        });
    }
}
