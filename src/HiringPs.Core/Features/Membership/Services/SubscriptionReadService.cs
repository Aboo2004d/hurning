using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قراءة الاشتراكات للإدارة عبر إسقاطات تحافظ على فلتر بيئة الدفع في DbContext.</summary>
public sealed class SubscriptionReadService(HiringDb db)
{
    /// <summary>يبني قائمة الإدارة مع البحث والحالة والترتيب قبل ترقيم الصفحات.</summary>
    public IQueryable<SubscriptionSummaryDto> AdminSubscriptions(string? q, string? status)
    {
        var now = DateTimeOffset.UtcNow;
        var query = db.MemberSubscriptions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.UserId == q || db.MemberProfiles.Any(m =>
                m.UserId == x.UserId && (m.Name.Contains(q) || m.Mobile.Contains(q))));
        query = status switch
        {
            "current" => query.Where(x => !x.Cancelled && x.StartsAt <= now && x.EndsAt > now),
            "scheduled" => query.Where(x => !x.Cancelled && x.StartsAt > now),
            "expired" => query.Where(x => !x.Cancelled && x.EndsAt <= now),
            "cancelled" => query.Where(x => x.Cancelled),
            _ => query,
        };
        return query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new SubscriptionSummaryDto
            {
                Id = x.Id, UserId = x.UserId, CreatedAt = x.CreatedAt,
                StartsAt = x.StartsAt, EndsAt = x.EndsAt, Cancelled = x.Cancelled,
                PlanVersion = new MembershipPlanLabelDto(x.PlanVersion.Id,
                    x.PlanVersion.NameAr, x.PlanVersion.NameEn),
            });
    }

    /// <summary>يعيد أسماء النسخ الحالية للاختيار عند منح اشتراك إداري.</summary>
    public Task<List<MembershipPlanLabelDto>> CurrentPlansAsync(CancellationToken ct) =>
        db.MembershipPlanVersions.AsNoTracking()
            .Where(x => db.MembershipPlans.Any(p => p.CurrentVersionId == x.Id))
            .Select(x => new MembershipPlanLabelDto(x.Id, x.NameAr, x.NameEn)).ToListAsync(ct);
}
