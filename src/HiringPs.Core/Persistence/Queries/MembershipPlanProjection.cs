using System.Linq.Expressions;

namespace HiringPs.Core;

/// <summary>إسقاط شروط نسخة الباقة إلى عقد عرض دون تمرير كيان EF إلى Razor.</summary>
public static class MembershipPlanProjection
{
    public static readonly Expression<Func<MembershipPlanVersion, MembershipPlanVersionSummaryDto>> Summary = x => new()
    {
        Id = x.Id, PlanId = x.PlanId, NameAr = x.NameAr, NameEn = x.NameEn,
        DescriptionAr = x.DescriptionAr, DescriptionEn = x.DescriptionEn,
        BadgeAr = x.BadgeAr, BadgeEn = x.BadgeEn, PriceIls = x.PriceIls,
        Days = x.Days, DetailKinds = x.DetailKinds, AlertKinds = x.AlertKinds,
    };
}
