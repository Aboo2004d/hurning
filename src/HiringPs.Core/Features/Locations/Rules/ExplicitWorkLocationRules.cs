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
        if (HasForeignLocation(value))
            return false;
        return LocationSearch.Groups.SelectMany(x => x).Any(x =>
            normalized.Contains(" " + LocationSearch.Normalize(x) + " ", StringComparison.Ordinal))
            || LocationCatalog.Matches(value, approved);
    }

    /// <summary>يكشف ذكرًا صريحًا لمدينة أو دولة أجنبية معروفة دون اعتباره بحثًا جغرافيًا شاملًا.</summary>
    public static bool HasForeignLocation(string value) => Regex.IsMatch(" " + LocationSearch.Normalize(value) + " ",
        @"(?:^|\s)(?:مصر|القاهرة|الاردن|عمان|لبنان|بيروت|سوريا|دمشق|السعودية|دبي|الامارات|egypt|cairo|jordan|lebanon|syria|kentucky|nebraska|illinois|usa|canada|australia)(?:\s|$)", RegexOptions.IgnoreCase);

    /// <summary>يفرق بين اختلاف وصف المنطقة وبين عنواني شارع متعارضين؛ لا يستخدم هذا التوسيع لدمج الأدوار.</summary>
    public static bool ConsistentScope(string first, string second)
    {
        if (HasForeignLocation(first) != HasForeignLocation(second)) return false;
        if (Compatible(first, second)) return true;
        if (HasForeignLocation(first) || HasForeignLocation(second)) return false;
        var a = Cities(first); var b = Cities(second);
        return a.Count > 0 && a.SetEquals(b) && (Broad(first) || Broad(second));
    }

    /// <summary>يجمع أسماء المدينة وصيغها المعروفة، مع اعتبار رام الله والبيرة منطقة حضرية واحدة لهذا الفحص فقط.</summary>
    private static HashSet<int> Cities(string value) => LocationSearch.Groups.Take(13).Select((names, id) => (names, id))
        .Where(x => x.names.Any(n => (" " + LocationSearch.Normalize(value) + " ").Contains(" " + LocationSearch.Normalize(n) + " ", StringComparison.Ordinal)))
        .Select(x => x.id == 1 ? 0 : x.id).ToHashSet();

    /// <summary>يحدد وصف مدينة/منطقة واسعًا لا يتضمن تفاصيل شارع أو معلم أو حي متعارض.</summary>
    private static bool Broad(string value)
    {
        var text = " " + LocationSearch.Normalize(value) + " ";
        foreach (var name in LocationSearch.Groups.SelectMany(x => x).OrderByDescending(x => x.Length))
        {
            var phrase = LocationSearch.Normalize(name);
            text = text.Replace(" " + phrase + " ", " ").Replace(" و" + phrase + " ", " ");
        }
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] descriptors = ["و", "منطقة", "ومنطقة", "الوسط", "والوسط", "محافظة", "مدينة", "and", "area", "central", "district", "region", "governorate", "fo", "field", "office"];
        return words.All(x => descriptors.Contains(x, StringComparer.OrdinalIgnoreCase));
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
