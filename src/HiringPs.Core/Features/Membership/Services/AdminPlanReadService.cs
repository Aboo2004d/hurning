using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قراءة إدارة الباقات عبر إسقاطات الشروط دون تمرير كيانات إلى العرض.</summary>
public sealed class AdminPlanReadService(HiringDb db)
{
    /// <summary>يعيد الباقات بترتيب الإدارة مع شروط نسختها الحالية قبل الترقيم.</summary>
    public IQueryable<AdminPlanSummaryDto> Plans() =>
        from plan in db.MembershipPlans.AsNoTracking()
        join version in db.MembershipPlanVersions.AsNoTracking() on plan.CurrentVersionId equals (int?)version.Id
        orderby plan.SortOrder, plan.Id
        select new AdminPlanSummaryDto(plan.Id, plan.Visible, new MembershipPlanVersionSummaryDto
        {
            Id = version.Id, PlanId = version.PlanId, NameAr = version.NameAr, NameEn = version.NameEn,
            PriceIls = version.PriceIls, Days = version.Days, MaxPreferredCategories = version.MaxPreferredCategories,
        });

    /// <summary>يقرأ بيانات النموذج ورمز التزامن من الخادم لتعديل باقة موجودة.</summary>
    public async Task<PlanEditDataDto?> EditAsync(int id, CancellationToken ct)
    {
        var plan = await db.MembershipPlans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (plan is null) return null;
        var version = await db.MembershipPlanVersions.AsNoTracking()
            .Where(x => x.Id == plan.CurrentVersionId).Select(MembershipPlanProjection.Summary).SingleAsync(ct);
        return new(plan.Id, plan.Visible, plan.SortOrder, Convert.ToBase64String(plan.Version), version);
    }
}
