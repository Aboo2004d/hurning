using HiringPs.Core;
using HiringPs.Web.Models;

public static class CheckoutStateChecks
{
    public static void Run()
    {
        void Check(bool value, string message)
        {
            if (!value)
                throw new Exception("FAIL: " + message);
            Console.WriteLine("PASS: " + message);
        }
        var order = new PaymentOrder
        {
            Gateway = "Lahza",
            State = PaymentState.AwaitingPayment,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
        };
        var fresh = PaymentActions.For(order, true);
        Check(
            fresh.CanPay && !fresh.CanCheck && !fresh.CanReport && !fresh.CanRetry,
            "Fresh checkout offers payment without verification, duplicate retry or debit complaint"
        );
        order.LastCheckedAt = DateTimeOffset.UtcNow; // Worker checks do not prove payment was attempted.
        Check(
            !PaymentActions.For(order, true).CanReport,
            "Worker check before checkout does not expose debit complaint"
        );
        order.CheckoutStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var returned = PaymentActions.For(order, true);
        Check(
            returned.CanCheck && returned.CanReport && !returned.CanPay && !returned.CanRetry,
            "Checked hosted checkout can verify and report an unresolved payment"
        );
        order.LastCheckedAt = null;
        Check(
            !PaymentActions.For(order, true).CanReport
                && PaymentActions.For(order, true, returned: true).CanReport,
            "Hosted debit complaint requires return or verification after the checkout attempt"
        );
        order.Gateway = "JawwalPay";
        Check(
            !PaymentActions.For(order, true).CanCheck && !PaymentActions.For(order, true).CanReport,
            "Wallet phone and OTP steps do not expose payment verification or debit complaint"
        );
        var uncertain = PaymentActions.For(order, true, walletConfirmationStarted: true);
        Check(
            !uncertain.CanPay && !uncertain.CanRetry && uncertain.CanCheck && uncertain.CanReport,
            "Uncertain wallet debit offers recovery without a second debit or new order"
        );
        order.State = PaymentState.Failed;
        var rejected = PaymentActions.For(
            order,
            true,
            walletConfirmationStarted: true,
            walletRejected: true
        );
        Check(
            !rejected.CanPay && !rejected.CanCheck && !rejected.CanReport && rejected.CanRetry,
            "Explicit wallet rejection is distinct from a possibly successful debit"
        );
        order.State = PaymentState.Paid;
        Check(
            PaymentActions.For(order, true, true) == new PaymentActions(false, false, false, false),
            "Paid order has no pay/check/complaint/retry controls"
        );
        order.State = PaymentState.ManuallyApproved;
        Check(
            PaymentActions.For(order, true, true) == new PaymentActions(false, false, false, false),
            "Manually activated subscription has no redundant payment controls"
        );
        order.State = PaymentState.Cancelled;
        Check(
            !PaymentActions.For(order, true).CanPay && !PaymentActions.For(order, true).CanCheck,
            "Cancelled unconfirmed wallet order cannot pay or poll a nonexistent debit"
        );
        order.State = PaymentState.Pending;
        order.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        Check(
            !PaymentActions.For(order, true).CanPay && PaymentActions.For(order, true).CanRetry,
            "Expired unpaid order requires a new checkout"
        );
    }
}
