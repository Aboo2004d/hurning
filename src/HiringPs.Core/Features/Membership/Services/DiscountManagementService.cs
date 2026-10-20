using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قراءة الخصومات وحفظها مع حماية شروط الخصم بعد ارتباطه بطلب دفع.</summary>
public sealed class DiscountManagementService(HiringDb db, MembershipService membership)
{
    /// <summary>يعيد خصومات الإدارة مرتبة قبل الترقيم عبر DTOs.</summary>
    public IQueryable<DiscountSummaryDto> Discounts() => db.DiscountCodes.AsNoTracking()
        .OrderByDescending(x => x.Id).Select(x => new DiscountSummaryDto
        {
            Id = x.Id, Code = x.Code, PlanId = x.PlanId, Percent = x.Percent,
            ExpiresAt = x.ExpiresAt, MaximumUses = x.MaximumUses, Enabled = x.Enabled,
        });

    /// <summary>يعيد نسخ جميع الباقات الحالية لاختيار تقييد الخصم، بما فيها غير المرئية للبيع.</summary>
    public Task<List<MembershipPlanVersionSummaryDto>> PlansAsync(CancellationToken ct) =>
        db.MembershipPlanVersions.AsNoTracking()
            .Where(x => db.MembershipPlans.Any(p => p.CurrentVersionId == x.Id))
            .Select(MembershipPlanProjection.Summary).ToListAsync(ct);

    /// <summary>يقرأ كودًا موجودًا لتجهيز نموذج تعديله.</summary>
    public Task<DiscountSummaryDto?> FindAsync(int id, CancellationToken ct) =>
        Discounts().SingleOrDefaultAsync(x => x.Id == id, ct);

    /// <summary>يحفظ حقول الخصم المسموحة ويرفض تغيير هوية أو نسبة أو باقة خصم مستخدم.</summary>
    public async Task<DiscountSaveOutcome> SaveAsync(DiscountSaveCommandDto input, CancellationToken ct)
    {
        if (input.Id < 0 || input.Percent is < 0 or > 100 || input.MaximumUses < 1
            || string.IsNullOrWhiteSpace(input.Code) || input.Code.Length > 50)
            return DiscountSaveOutcome.InvalidRequest;
        if (
            input.PlanId is <= 0
            || input.PlanId is int planId
                && !await db.MembershipPlans.AnyAsync(x => x.Id == planId, ct)
        )
            return DiscountSaveOutcome.InvalidRequest;
        input = input with { Code = input.Code.Trim().ToUpperInvariant() };
        var code =
            input.Id == 0
                ? new DiscountCode()
                : await db.DiscountCodes.SingleOrDefaultAsync(x => x.Id == input.Id, ct);
        if (code is null)
            return DiscountSaveOutcome.NotFound;
        if (input.Id == 0)
            db.DiscountCodes.Add(code);
        else if (
            await db.PaymentOrders.AnyAsync(x => x.DiscountCodeId == code.Id, ct)
            && (
                code.Code != input.Code
                || code.Percent != input.Percent
                || code.PlanId != input.PlanId
            )
        )
        {
            return DiscountSaveOutcome.UsedImmutable;
        }
        if (await db.DiscountCodes.AnyAsync(x => x.Code == input.Code && x.Id != input.Id, ct))
            return DiscountSaveOutcome.InvalidRequest;
        code.Code = input.Code;
        code.Percent = input.Percent;
        code.PlanId = input.PlanId;
        code.ExpiresAt = input.ExpiresAt;
        code.MaximumUses = input.MaximumUses;
        code.Enabled = input.Enabled;
        membership.Audit(
            input.Actor,
            "Discount.Save",
            $"Code {input.Code}; enabled={code.Enabled}"
        );
        await db.SaveChangesAsync(ct);
        return DiscountSaveOutcome.Saved;
    }
}
