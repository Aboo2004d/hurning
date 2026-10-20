using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>ينسق بدء الدفع المستضاف وملكية الطلب وحجزه؛ لا يؤكد دفعة من عودة المتصفح.</summary>
public sealed class HostedPaymentCheckout(HiringDb db, PaymentGatewayRegistry registry)
{
    /// <summary>يحجز تهيئة لحظة مرة واحدة، ويعيد رابطًا مثبتًا أو نتيجة انتظار؛ يوجه المحفظة إلى مسارها المستقل.</summary>
    public async Task<PaymentCheckoutResult> StartAsync(Guid id, string owner, CancellationToken ct)
    {
        var order = await db.PaymentOrders.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == owner,
            ct
        );
        if (order is null)
            return new(PaymentCheckoutRoute.NotFound);
        if (
            MembershipRules.Paid(order.State)
            || order.ExpiresAt <= DateTimeOffset.UtcNow
            || order.State is PaymentState.Failed or PaymentState.Cancelled
        )
        {
            return new(PaymentCheckoutRoute.Details, "PaymentExpired");
        }
        if (order.GatewayEnvironment != db.ActivePaymentEnvironment)
        {
            return new(PaymentCheckoutRoute.Details, "PaymentEnvironmentMismatch");
        }
        var gateway = (await registry.AvailableAsync(ct)).SingleOrDefault(x =>
            x.Key == order.Gateway
        );
        if (gateway is null)
        {
            return new(PaymentCheckoutRoute.Details, "GatewayUnavailable");
        }
        if (gateway.Key == "JawwalPay")
            return new(PaymentCheckoutRoute.Wallet);
        // Claim once, before the HTTP call. A timeout must never create another charge.
        if (order.CheckoutUrl is not null)
        {
            if (
                Uri.TryCreate(order.CheckoutUrl, UriKind.Absolute, out var saved)
                && LahzaGateway.IsCheckoutUrl(saved)
            )
                return new(PaymentCheckoutRoute.External, Url: saved.AbsoluteUri);
            return new(PaymentCheckoutRoute.InvalidRequest);
        }
        var claimed = await db
            .PaymentOrders.Where(x =>
                x.Id == id
                && x.UserId == owner
                && x.CheckoutStartedAt == null
                && x.ExpiresAt > DateTimeOffset.UtcNow
                && x.State == PaymentState.AwaitingPayment
            )
            .ExecuteUpdateAsync(
                update =>
                    update
                        .SetProperty(x => x.CheckoutStartedAt, DateTimeOffset.UtcNow)
                        .SetProperty(x => x.ProviderReference, LahzaGateway.Reference(id))
                        .SetProperty(x => x.State, PaymentState.Pending),
                ct
            );
        if (claimed == 0)
        {
            return new(PaymentCheckoutRoute.Details, "PaymentInitializationPending");
        }
        await db.Entry(order).ReloadAsync(ct);
        try
        {
            var checkout = await gateway.CheckoutAsync(order, ct);
            if (
                !LahzaGateway.IsCheckoutUrl(checkout.Url)
                || checkout.Reference != order.ProviderReference
            )
                throw new PaymentProviderException();
            await db
                .PaymentOrders.Where(x => x.Id == id)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(x => x.CheckoutUrl, checkout.Url.AbsoluteUri),
                    ct
                );
            return new(PaymentCheckoutRoute.External, Url: checkout.Url.AbsoluteUri);
        }
        catch (PaymentProviderException)
        {
            return new(PaymentCheckoutRoute.Details, "PaymentInitializationPending");
        }
    }
}
