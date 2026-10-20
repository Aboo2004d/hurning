using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>صلاحيات عرض إجراءات الدفع؛ تفوض حساب القواعد إلى سياسة Core المشتركة.</summary>
public sealed record PaymentActions(bool CanPay, bool CanCheck, bool CanReport, bool CanRetry)
{
    /// <summary>يحافظ على توافق مسارات العرض الحالية التي تمرر كيانًا للقراءة.</summary>
    public static PaymentActions For(PaymentOrder order, bool ready, bool walletConfirmationStarted = false,
        bool walletRejected = false, bool returned = false) =>
        From(PaymentActionRules.For(new(order.State, order.Gateway, order.ExpiresAt,
            order.CheckoutStartedAt, order.LastCheckedAt), ready, walletConfirmationStarted, walletRejected, returned));

    /// <summary>يحسب إجراءات صفحة الدفع من DTO للقراءة دون تمرير كيان قاعدة إلى العرض.</summary>
    public static PaymentActions For(PaymentOrderSummaryDto order, bool ready, bool walletConfirmationStarted = false,
        bool walletRejected = false, bool returned = false) =>
        From(PaymentActionRules.For(new(order.State, order.Gateway, order.ExpiresAt,
            order.CheckoutStartedAt, order.LastCheckedAt), ready, walletConfirmationStarted, walletRejected, returned));

    /// <summary>يحول نتيجة سياسة المجال إلى النموذج الذي تستخدمه الواجهة.</summary>
    private static PaymentActions From(PaymentActionOptions options) =>
        new(options.CanPay, options.CanCheck, options.CanReport, options.CanRetry);
}
