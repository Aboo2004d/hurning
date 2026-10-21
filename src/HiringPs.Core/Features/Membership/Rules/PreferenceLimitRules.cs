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

    /// <summary>يحسب المجموعة المعتمدة وحدة واحدة، ويبقي المجالات المستقلة منفصلة دون احتساب اختلاف التنسيق.</summary>
    public static int Count(IEnumerable<string> categories, CategoryGroupMap? groups = null) => categories
        .Where(x => !string.IsNullOrWhiteSpace(x)).Select(value => (groups ?? CategoryGroupMap.Empty).Identity(value))
        .Distinct(StringComparer.OrdinalIgnoreCase).Count();

    /// <summary>لا تنبيهات فرص دون مجال مختار حتى للباقة غير المحدودة؛ يمنع تجاوز الحد.</summary>
    public static bool AllowsAlerts(IEnumerable<string> categories, int? limit, CategoryGroupMap? groups = null) =>
        Count(categories, groups) is var count && count > 0 && (limit is null || count <= limit);
}
