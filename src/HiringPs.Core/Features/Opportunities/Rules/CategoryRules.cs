namespace HiringPs.Core;

/// <summary>اسم مجال واحد ومقارنته دون تحويل الاقتراح التقريبي إلى اختيار تلقائي.</summary>
public static class CategoryRules
{
    public const int NewCategoryMaxLength = 200;

    /// <summary>يُبقي الاسم الأصلي مع توحيد المسافات فقط.</summary>
    public static string Clean(string? value) => string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>يوحد اختلاف الأحرف والمسافات وأشكال الألف للمقارنة، دون دمج مسميات مختلفة معنويًا.</summary>
    public static string Key(string? value) => TextRules.Normalize(Clean(value));

    /// <summary>يختار التسمية الموجودة عند التطابق فقط، وإلا يبقي المدخل الجديد دون تخمين.</summary>
    public static string Resolve(string? value, IEnumerable<string> existing)
    {
        var cleaned = Clean(value);
        var key = Key(cleaned);
        return existing.OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault(x => Key(x) == key) ?? cleaned;
    }

    /// <summary>يتحقق من صلاحية تسمية جديدة أدخلها المستخدم.</summary>
    public static bool ValidNew(string? value) => Clean(value).Length is > 0 and <= NewCategoryMaxLength
        && !(value ?? "").Any(c => char.IsControl(c) && !char.IsWhiteSpace(c));
}
