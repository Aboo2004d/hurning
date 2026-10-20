using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يوحد شروط معاينة الخصم وحجزه مع عزل استخداماته حسب بيئة الدفع.</summary>
public sealed class MembershipDiscountService(HiringDb db)
{
    /// <summary>يتحقق من تفعيل الكود وصلاحيته وحد الاستخدام وعدم استخدام العضو له؛ لا يحجزه وحده.</summary>
    public async Task<MembershipDiscountDto?> FindAsync(string value, int planId, string owner, CancellationToken ct)
    {
        var code = value.Trim().ToUpperInvariant();
        var discount = await db
            .DiscountCodes.AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Code == code
                    && (x.PlanId == null || x.PlanId == planId)
                    && x.Enabled
                    && x.ExpiresAt > DateTimeOffset.UtcNow,
                ct
            );
        if (discount is null)
            return null;
        var reservations = db.PaymentOrders.Where(x =>
            x.DiscountCodeId == discount.Id && x.GatewayEnvironment == db.ActivePaymentEnvironment
        );
        if (
            await reservations.CountAsync(ct) >= discount.MaximumUses
            || await reservations.AnyAsync(x => x.UserId == owner, ct)
        )
            return null;
        return new MembershipDiscountDto(discount.Id, discount.Percent);
    }

}
