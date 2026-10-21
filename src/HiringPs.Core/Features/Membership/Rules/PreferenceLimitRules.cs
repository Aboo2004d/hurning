namespace HiringPs.Core;

/// <summary>قواعد عدد المجالات المفضلة عند الجمع بين الاشتراكات السارية.</summary>
public static class PreferenceLimitRules
{
    /// <summary>يعتمد أكبر حد متاح؛ الاشتراك غير المحدود أو غياب الاشتراكات يبقي التفضيلات غير محدودة.</summary>
    public static int? Effective(IEnumerable<int?> limits)
    {
        var values = limits.ToArray();
        return values.Length == 0 || values.Any(x => x is null) ? null : values.Max();
    }

    /// <summary>يحسب المجالات المختلفة دون احتساب اختلاف حالة الأحرف أو المسافات الخارجية.</summary>
    public static int Count(IEnumerable<string> categories) => categories
        .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase).Count();

    /// <summary>يمنع تجاوز الحد؛ ترك المجالات فارغة لا يعني جميع المجالات في الباقات المحدودة.</summary>
    public static bool AllowsAlerts(IEnumerable<string> categories, int? limit) =>
        limit is null || Count(categories) is var count && count > 0 && count <= limit;
}
