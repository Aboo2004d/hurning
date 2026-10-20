using System.Text;
using System.Text.RegularExpressions;

namespace HiringPs.Core;

/// <summary>يطبع أسماء المواقع ويبني تمثيلها القابل للبحث.</summary>
public static class LocationSearch
{
    public static readonly string[][] Groups =
    [
        ["رام الله", "Ramallah", "Ram Allah"],
        ["البيرة", "Al Bireh", "Al-Bireh", "El Bireh"],
        ["غزة", "Gaza", "Gaza Strip"],
        ["القدس", "Jerusalem", "East Jerusalem", "القدس الشرقية"],
        ["الخليل", "Hebron", "Al Khalil"],
        ["نابلس", "Nablus"],
        ["جنين", "Jenin"],
        ["بيت لحم", "Bethlehem", "Bayt Lahm"],
        ["أريحا", "اريحا", "Jericho", "Ariha"],
        ["طولكرم", "Tulkarm", "Tulkarem"],
        ["قلقيلية", "Qalqilya", "Qalqilia"],
        ["طوباس", "Tubas"],
        ["سلفيت", "Salfit"],
        ["فلسطين", "Palestine"],
        ["الضفة الغربية", "West Bank"],
    ];

    /// <summary>يطبع النص للمقارنة والبحث مع الحفاظ على قواعد المجال الحالية.</summary>
    public static string Normalize(string text)
    {
        text = text.Normalize(NormalizationForm.FormKC)
            .ToLowerInvariant()
            .Replace('أ', 'ا')
            .Replace('إ', 'ا')
            .Replace('آ', 'ا')
            .Replace('ى', 'ي')
            .Replace("ـ", "");
        text = Regex.Replace(text, @"[\u064B-\u065F\u0670]", "");
        return Regex.Replace(text, @"[^\p{L}\p{N}]+", " ").Trim();
    }

    /// <summary>ينشئ النص الموحد المستخدم لفهرسة مواقع الفرصة.</summary>
    public static string Index(string location)
    {
        var text = Normalize(location);
        var aliases = Groups
            .Where(group =>
                group.Any(alias =>
                    (" " + text + " ").Contains(
                        " " + Normalize(alias) + " ",
                        StringComparison.Ordinal
                    )
                )
            )
            .SelectMany(group => group)
            .Select(Normalize);
        return text + " " + string.Join(' ', aliases.Distinct());
    }
}
