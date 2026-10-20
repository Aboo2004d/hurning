namespace HiringPs.Core;

/// <summary>سياسة إجراءات الدفع المشتركة بين العرض وطلب المراجعة؛ لا تعتمد دفعات ولا تتصل ببوابة.</summary>
public static class PaymentActionRules
{
    /// <summary>يحسب الإجراءات المتاحة من حالة الطلب وحجز المحفظة والعودة من المزود وفق القواعد الحالية.</summary>
    public static PaymentActionOptions For(
        PaymentActionInput order,
        bool ready,
        bool walletConfirmationStarted = false,
        bool walletRejected = false,
        bool returned = false
    )
    {
        if (MembershipRules.Paid(order.State))
            return new(false, false, false, false);

        var wallet = order.Gateway == "JawwalPay";
        var uncertain = wallet && walletConfirmationStarted && !walletRejected;
        var hostedAttempt =
            !wallet && order.Gateway == "Lahza" && order.CheckoutStartedAt is not null;
        var open = order.State is PaymentState.AwaitingPayment or PaymentState.Pending;
        var canPay =
            ready
            && open
            && order.ExpiresAt > DateTimeOffset.UtcNow
            && !uncertain
            && !hostedAttempt;
        var canCheck = uncertain || hostedAttempt;
        var checkedHosted = hostedAttempt && order.LastCheckedAt >= order.CheckoutStartedAt;
        return new(
            canPay,
            canCheck,
            uncertain || hostedAttempt && (returned || checkedHosted),
            !uncertain
                && !(hostedAttempt && open)
                && !canPay
                && (
                    order.ExpiresAt <= DateTimeOffset.UtcNow
                    || order.State is PaymentState.Failed or PaymentState.Cancelled
                )
        );
    }
}
