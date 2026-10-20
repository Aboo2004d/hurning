using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يقرأ الباقات المرئية وفترات العضو مع احترام بيئة الدفع ونسخة الشراء.</summary>
public sealed class MemberPlanReadService(HiringDb db)
{
    /// <summary>يرتب الباقات المرئية ثم يربط كل باقة بنسختها الحالية بنفس ترتيب العرض.</summary>
    public async Task<List<MembershipPlanVersionSummaryDto>> CatalogAsync(CancellationToken ct)
    {
        var plans = await db.MembershipPlans.AsNoTracking()
            .Where(x => x.Visible && x.CurrentVersionId != null)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.CurrentVersionId }).ToListAsync(ct);
        var ids = plans.Select(x => x.CurrentVersionId!.Value).ToList();
        var versions = await db.MembershipPlanVersions.AsNoTracking()
            .Where(x => ids.Contains(x.Id)).Select(MembershipPlanProjection.Summary).ToListAsync(ct);
        return plans.Select(x => versions.Single(v => v.Id == x.CurrentVersionId)).ToList();
    }

    /// <summary>يعيد النسخة الحالية للباقة المرئية فقط؛ لا يستخدم قيمة السعر الواردة من النموذج.</summary>
    public async Task<MembershipPlanVersionSummaryDto?> CurrentAsync(int planId, CancellationToken ct)
    {
        var plan = await db.MembershipPlans.AsNoTracking()
            .Where(x => x.Id == planId && x.Visible)
            .Select(x => new { x.CurrentVersionId }).SingleOrDefaultAsync(ct);
        return plan is null ? null : await db.MembershipPlanVersions.AsNoTracking()
            .Where(x => x.Id == plan.CurrentVersionId).Select(MembershipPlanProjection.Summary)
            .SingleOrDefaultAsync(ct);
    }

    /// <summary>يحسب بداية تجديد بعد آخر نهاية غير ملغاة، أو الآن إذا لم توجد فترة لاحقة.</summary>
    public async Task<DateTimeOffset> StartsAsync(string owner, DateTimeOffset now, CancellationToken ct)
    {
        var ends = await db.MemberSubscriptions.AsNoTracking()
            .Where(x => x.UserId == owner && !x.Cancelled && x.EndsAt > now)
            .Select(x => x.EndsAt).ToListAsync(ct);
        return ends.Append(now).Max();
    }

    /// <summary>يعيد فترات العضو السارية والمجدولة بشروط نسخها الأصلية للعرض فقط.</summary>
    public async Task<List<MemberSubscriptionPeriodDto>> PeriodsAsync(string owner, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var periods = await db.MemberSubscriptions.AsNoTracking()
            .Where(x => x.UserId == owner && !x.Cancelled && x.EndsAt > now)
            .OrderBy(x => x.StartsAt)
            .Select(x => new { x.PlanVersionId, x.StartsAt, x.EndsAt }).ToListAsync(ct);
        var ids = periods.Select(x => x.PlanVersionId).Distinct().ToList();
        var versions = await db.MembershipPlanVersions.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Select(MembershipPlanProjection.Summary).ToDictionaryAsync(x => x.Id, ct);
        return periods.Select(x => new MemberSubscriptionPeriodDto(x.PlanVersionId,
            versions[x.PlanVersionId], x.StartsAt, x.EndsAt)).ToList();
    }
}
