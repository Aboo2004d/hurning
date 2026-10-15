using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace HiringPs.Core;
public static class TextEvidence
{
    // Formatting differences only. Never accept a quotation merely because it is semantically similar.
    public static string Normalize(string value)
    {
        var text = WebUtility.HtmlDecode(value).Normalize(NormalizationForm.FormKC).Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace("ـ", "");
        text = Regex.Replace(text, "[\\u064B-\\u065F\\u0670\\u200B-\\u200F\\u202A-\\u202E\\uFEFF]", "");
        text = text.Replace('–', '-').Replace('—', '-').Replace('’', '\'');
        text = Regex.Replace(text, @"\s*/\s*", "/");
        text = Regex.Replace(text, @"(?<!\S)و\s+(?=ال)", "و");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    public static bool Contains(string source, string quotation) => !string.IsNullOrWhiteSpace(quotation) && Normalize(source).Contains(Normalize(quotation), StringComparison.OrdinalIgnoreCase);
    public static bool TitleContains(string source, string title)
    {
        if (Contains(source, title))
            return true;
        var parts = Regex.Split(title, @"\s+[-–—]\s+|[(),،]").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        return parts.Length > 1 && parts.All(x => Contains(source, x.Trim()));
    }

    public static bool OrganizationContains(string source, string organization)
    {
        if (Contains(source, organization))
            return true;
        static string NameText(string value) => Regex.Replace(Regex.Replace(Normalize(value), @"[^\p{L}\p{N}\s]", " "), @"\s+", " ").Trim();
        var normalizedSource = " " + NameText(source) + " ";
        var fullName = NameText(organization);
        if (fullName.Length > 0 && normalizedSource.Contains(" " + fullName + " ", StringComparison.OrdinalIgnoreCase))
            return true;
        var parts = Regex.Split(Normalize(organization), @"\s*/\s*|\s*[-–—]\s*|[()]|(?<=[\u0600-\u06FF])\s+(?=[A-Za-z])|(?<=[A-Za-z])\s+(?=[\u0600-\u06FF])").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        // An original bilingual name or acronym is sufficient; never require a translation to occur too.
        return parts.Any(part =>
        {
            var name = Regex.Replace(part, @"^(?:شركة|مؤسسة|جمعية|Company)\s+", "", RegexOptions.IgnoreCase);
            name = NameText(name);
            if (Regex.IsMatch(name, @"\A(?:شركة|مؤسسة|جمعية|company|organization|organisation)\z", RegexOptions.IgnoreCase))
                return false;
            return name.Length >= 3 && normalizedSource.Contains(" " + name + " ", StringComparison.OrdinalIgnoreCase);
        });
    }
}
