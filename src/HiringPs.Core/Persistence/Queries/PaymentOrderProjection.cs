using System.Linq.Expressions;

namespace HiringPs.Core;

/// <summary>تعريف إسقاط SQL موحد لعرض الطلب؛ لا يحمّل كيان العضو أو بيانات جلسة المحفظة.</summary>
public static class PaymentOrderProjection
{
    /// <summary>حقول الطلب والباقة المسموحة لصفحات الدفع الحالية.</summary>
    public static Expression<Func<PaymentOrder, PaymentOrderSummaryDto>> For(HiringDb db) => order => new PaymentOrderSummaryDto
    {
        Id = order.Id, UserId = order.UserId,
        MemberName = db.MemberProfiles.Where(m => m.UserId == order.UserId).Select(m => m.Name).FirstOrDefault() ?? "", AmountIls = order.AmountIls, Gateway = order.Gateway,
        GatewayEnvironment = order.GatewayEnvironment, ProviderReference = order.ProviderReference,
        State = order.State, CreatedAt = order.CreatedAt, ExpiresAt = order.ExpiresAt,
        CheckoutStartedAt = order.CheckoutStartedAt, LastCheckedAt = order.LastCheckedAt,
        NeedsReconciliation = order.State != PaymentState.Paid && order.State != PaymentState.ManuallyApproved
            && order.State != PaymentState.Cancelled
            && (order.CheckoutStartedAt != null || (order.Gateway == "JawwalPay"
                && db.WalletPaymentSessions.Any(session => session.OrderId == order.Id
                    && session.ConfirmationStartedAt != null))),
        DecisionReason = order.DecisionReason, PaymentEvidence = order.PaymentEvidence,
        PlanVersion = new PaymentPlanSummaryDto
        {
            PlanId = order.PlanVersion.PlanId, NameAr = order.PlanVersion.NameAr,
            NameEn = order.PlanVersion.NameEn, Days = order.PlanVersion.Days
        }
    };
}
