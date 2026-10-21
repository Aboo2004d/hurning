namespace HiringPs.Core;

/// <summary>قواعد عدد المجالات المفضلة عند الجمع بين الاشتراكات السارية.</summary>
public static class PreferenceLimitRules
{
    public const int NonSubscriberLimit = 3;

    /// <summary>ثلاثة مجالات لغير المشترك؛ يعتمد أكبر حد للاشتراكات السارية، ويبقي الباقة غير المحدودة كذلك.</summary>
    public static int? Effective(IEnumerable<int?> limits)
    {
        var values = limits.ToArray();
        return values.Length == 0 ? NonSubscriberLimit : values.Any(x => x is null) ? null : values.Max();
    }

    /// <summary>يحسب المجالات المختلفة دون احتساب اختلاف حالة الأحرف أو المسافات الخارجية.</summary>
    public static int Count(IEnumerable<string> categories) => categories
        .Where(x => !string.IsNullOrWhiteSpace(x)).Select(CategoryRules.Key)
        .Distinct(StringComparer.OrdinalIgnoreCase).Count();

    /// <summary>لا تنبيهات فرص دون مجال مختار حتى للباقة غير المحدودة؛ يمنع تجاوز الحد.</summary>
    public static bool AllowsAlerts(IEnumerable<string> categories, int? limit) =>
        Count(categories) is var count && count > 0 && (limit is null || count <= limit);
}
