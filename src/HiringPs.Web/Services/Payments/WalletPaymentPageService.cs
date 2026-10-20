using HiringPs.Core;
using HiringPs.Web.Models;

namespace HiringPs.Web.Services.Payments;

/// <summary>يجمع نموذج عرض المحفظة من DTOs وحالة البوابة دون استقبال OTP أو الوصول إلى EF.</summary>
public sealed class WalletPaymentPageService(PaymentReadService payments, PaymentGatewayRegistry registry,
    JawwalPayGateway gateway, PaymentEnvironment environment)
{
    /// <summary>يتحقق من ملكية الطلب والبيئة ويحسب إجراءات المحفظة المتاحة دون تنفيذ خصم.</summary>
    public async Task<WalletCheckoutPage?> ReadAsync(Guid id, string owner, string activeEnvironment, CancellationToken ct)
    {
        var order = await payments.OrderAsync(id, owner, ct);
        if (order is null || order.Gateway != "JawwalPay" || order.GatewayEnvironment != activeEnvironment) return null;
        if (MembershipRules.Paid(order.State)) return new WalletCheckoutPage { Order = order };
        // Never pass the protected session token (or a posted OTP) to a view.
        var session = await payments.WalletAsync(id, owner, ct);
        var now = DateTimeOffset.UtcNow;
        var available = (await registry.AvailableAsync(ct)).Any(x => x.Key == "JawwalPay");
        return new WalletCheckoutPage
        {
            Order = order,
            IsSandbox = environment.ShowDevelopmentHints,
            CanInitiate =
                session is null
                && available
                && order.ExpiresAt > now
                && order.State is PaymentState.AwaitingPayment or PaymentState.Pending,
            CanConfirm =
                gateway.Ready
                && session
                    is { HasKey: true, ConfirmationStartedAt: null, InitiationRejected: false }
                && session.ExpiresAt > now
                && order.ExpiresAt > now
                && order.State == PaymentState.Pending,
            CanCancel =
                !MembershipRules.Paid(order.State)
                && order.State != PaymentState.Cancelled
                && (session?.ConfirmationStartedAt is null || session.ConfirmationRejected),
            ConfirmationStarted = session?.ConfirmationStartedAt != null,
            Rejected =
                session?.ConfirmationRejected == true
                || session?.InitiationRejected == true
                || order.State == PaymentState.Failed,
            PhoneHint = session?.PhoneHint,
            ExpiresAt = session?.ExpiresAt,
        };
    }
}
