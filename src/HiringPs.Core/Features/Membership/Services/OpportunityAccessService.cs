using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قراءة وحفظ قواعد إتاحة تفاصيل الفرص مع تسجيل الفاعل.</summary>
public sealed class OpportunityAccessService(HiringDb db, MembershipService membership)
{
    /// <summary>يعيد قواعد الوصول حسب نوع الفرصة للعرض.</summary>
    public Task<List<OpportunityAccessSummaryDto>> ReadAsync(CancellationToken ct) =>
        db.OpportunityAccessRules.AsNoTracking().OrderBy(x => x.Kind)
            .Select(x => new OpportunityAccessSummaryDto(x.Kind, x.PublicDetails)).ToListAsync(ct);

    /// <summary>يغير الإتاحة للأنواع المعروفة فقط ويحفظ التدقيق مع التغييرات.</summary>
    public async Task<bool> SaveAsync(OpportunityKind[] publicKinds, string actor, CancellationToken ct)
    {
        if (publicKinds.Any(x => !Enum.IsDefined(x))) return false;
        foreach (var rule in await db.OpportunityAccessRules.ToListAsync(ct))
            rule.PublicDetails = publicKinds.Contains(rule.Kind);
        membership.Audit(actor, "Access.Update", string.Join(',', publicKinds));
        await db.SaveChangesAsync(ct);
        return true;
    }
}
