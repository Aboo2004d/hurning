using System.Text.RegularExpressions;
namespace HiringPs.Core;

/// <summary>يتحقق من مكان عمل مقتبس من المصدر؛ لا يستخدم مقر المؤسسة أو تخمينًا من الملخص.</summary>
public static class ExplicitWorkLocationRules
{
    /// <summary>يثبت اسمًا فلسطينيًا معروفًا بحدود الكلمات أو قيمة اعتمدتها الإدارة.</summary>
    public static bool KnownLocal(string value, IEnumerable<string> approved)
    {
        var normalized = " " + LocationSearch.Normalize(value) + " ";
        // A known local name must not override an explicitly conflicting foreign location.
        if (Regex.IsMatch(normalized, @"(?:^|\s)(?:مصر|القاهرة|الاردن|عمان|لبنان|بيروت|سوريا|دمشق|السعودية|دبي|الامارات|egypt|cairo|jordan|lebanon|syria|kentucky|nebraska|illinois|usa|canada|australia)(?:\s|$)", RegexOptions.IgnoreCase))
            return false;
        return LocationSearch.Groups.SelectMany(x => x).Any(x =>
            normalized.Contains(" " + LocationSearch.Normalize(x) + " ", StringComparison.Ordinal))
            || LocationCatalog.Matches(value, approved);
    }

    /// <summary>يقارن أسماء المكان للمطابقة داخل الإعلان نفسه، دون تحويل النص المعروض أو تغيير دليله.</summary>
    public static bool Compatible(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second)) return true;
        var a = " " + LocationSearch.Normalize(first) + " ";
        var b = " " + LocationSearch.Normalize(second) + " ";
        // Containment permits a city and its full address, but not different districts or cities.
        return a == b || a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal);
    }
}
