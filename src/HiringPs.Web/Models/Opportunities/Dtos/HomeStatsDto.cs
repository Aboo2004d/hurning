using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>إحصاءات الواجهة المعتمدة: الفرص الحالية والجهات والإضافات خلال آخر 24 ساعة.</summary>
public sealed record HomeStatsDto(IReadOnlyDictionary<OpportunityKind, int> Counts, int Organizations, int NewToday)
{
    /// <summary>مجموع الفرص المفتوحة عبر جميع الأنواع.</summary>
    public int Total => Counts.Values.Sum();

    /// <summary>عدد فرص التبويب مع جمع العطاءات والمناقصات دون تغيير الأنواع الأصلية.</summary>
    public int GroupCount(OpportunityKind kind) => PublicListingKinds.Group(kind) == OpportunityKind.Tender
        ? Count(OpportunityKind.Tender) + Count(OpportunityKind.ProcurementNotice)
        : Count(kind);

    /// <summary>عدد الفرص المفتوحة لنوع واحد، أو صفر عند غيابه.</summary>
    public int Count(OpportunityKind kind) => Counts.TryGetValue(kind, out var count) ? count : 0;
}
