using System.Data;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>ينشئ طلب اشتراك ثابت الشروط ضمن قفل العضو وحجز الخصم ومعاملة Serializable.</summary>
public sealed class MembershipCheckoutService(HiringDb db, MembershipService membership,
    PaymentGatewayRegistry registry, MembershipDiscountService discounts)
{
    /// <summary>يعيد فحص النسخة والسعر ويثبت الطلب قبل إتاحة الدفع؛ لا يحدث أسعار الطلبات السابقة.</summary>
    public async Task<MembershipCheckoutResultDto> CreateAsync(MembershipCheckoutCommandDto input, CancellationToken ct)
    {
        if (input.RequestId == Guid.Empty) return new(MembershipCheckoutOutcome.InvalidRequest);
        var plan = await db.MembershipPlanVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == input.PresentedVersionId && x.PlanId == input.PlanId, ct);
        if (plan is null) return new(MembershipCheckoutOutcome.PlanChanged);
        Guid orderId;
        decimal reservedAmount;
        await using (
            var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct
            )
        )
        {
            var lockedMember = await membership.LockMemberAsync(input.Owner, ct);
            if (!lockedMember.Activated || lockedMember.Suspended)
                return new(MembershipCheckoutOutcome.Forbidden);
            var replay = await db.PaymentOrders.SingleOrDefaultAsync(
                x => x.Id == input.RequestId,
                ct
            );
            if (replay is not null)
                return replay.UserId == input.Owner && replay.GatewayEnvironment == db.ActivePaymentEnvironment
                    ? new(MembershipCheckoutOutcome.Completed, replay.Id)
                    : new(MembershipCheckoutOutcome.InvalidRequest);
            // Re-read the plan under the transaction: an edit cannot silently change the displayed terms.
            var current = await db.MembershipPlans.SingleAsync(x => x.Id == input.PlanId, ct);
            if (!current.Visible || current.CurrentVersionId != plan.Id)
            {
                return new(MembershipCheckoutOutcome.PlanChanged);
            }
            MembershipDiscountDto? discount = null;
            if (!string.IsNullOrWhiteSpace(input.DiscountCode))
            {
                discount = await discounts.FindAsync(input.DiscountCode, current.Id, input.Owner, ct);
                // The reservation remains attached even after expiration/cancellation, including late verification.
                if (discount is null)
                {
                    return new(MembershipCheckoutOutcome.DiscountInvalid);
                }
            }
            var amount = MembershipRules.Amount(plan.PriceIls, discount?.Percent ?? 0);
            // A concurrent discount edit must not silently change the amount accepted in the preview.
            if (amount != input.FinalAmount) return new(MembershipCheckoutOutcome.PlanChanged);
            if (
                amount > 0
                && !(await registry.AvailableAsync(ct)).Any(x =>
                    x.Key == input.Gateway && x.SupportsAmount(amount)
                )
            )
            {
                return new(MembershipCheckoutOutcome.GatewayUnavailable);
            }
            var now = DateTimeOffset.UtcNow;
            var order = new PaymentOrder
            {
                Id = input.RequestId,
                UserId = input.Owner,
                PlanVersionId = plan.Id,
                AmountIls = amount,
                DiscountCodeId = discount?.Id,
                DiscountPercent = discount?.Percent ?? 0,
                Gateway = amount == 0 ? "discount" : input.Gateway,
                GatewayEnvironment = db.ActivePaymentEnvironment,
                ExpiresAt = now.AddMinutes(10),
                TermsAcceptedAt = now,
            };
            db.PaymentOrders.Add(order);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            orderId = order.Id;
            reservedAmount = order.AmountIls;
        }
        if (reservedAmount == 0)
            await membership.ConfirmAsync(
                orderId,
                input.Owner,
                false,
                "Full discount",
                "Valid reserved discount",
                "discount:" + orderId,
                0,
                "ILS",
                ct
            );
        return new(MembershipCheckoutOutcome.Completed, orderId);
    }
}
