using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>إحصاءات الواجهة المعتمدة: الفرص الحالية والجهات والإضافات خلال آخر 24 ساعة.</summary>
public sealed record HomeStatsDto(IReadOnlyDictionary<OpportunityKind, int> Counts, int Organizations, int NewToday)
{
    /// <summary>مجموع الفرص المفتوحة عبر جميع الأنواع.</summary>
    public int Total => Counts.Values.Sum();

    /// <summary>عدد الفرص المفتوحة لنوع واحد، أو صفر عندما لا توجد فرص لهذا النوع.</summary>
    public int Count(OpportunityKind kind) => Counts.TryGetValue(kind, out var count) ? count : 0;
}
