using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

public sealed record GatewayCheck(
    bool Confirmed,
    Guid OrderId,
    string? Reference,
    decimal? Amount,
    string? Currency,
    PaymentState State,
    bool Inconclusive = false
);

public sealed record GatewayCheckout(string Reference, Uri Url);

// Only an adapter backed by the actual provider contract may report Ready.
// No guessed endpoints, fake successful payments or browser-return confirmations.
public interface IPaymentGateway
{
    string Key { get; }
    bool Ready { get; }
    string Environment => "Production";
    bool SupportsAmount(decimal amount) => amount > 0;
    Task<GatewayCheckout> CheckoutAsync(PaymentOrder order, CancellationToken ct);
    Task<GatewayCheck> CheckAsync(PaymentOrder order, CancellationToken ct);
}

public sealed class PaymentReconciliation(
    HiringDb db,
    MembershipService membership,
    PaymentGatewayRegistry registry
)
{
    public async Task<string> CheckAsync(
        Guid orderId,
        string? owner,
        string actor,
        CancellationToken ct
    )
    {
        var order = await db
            .PaymentOrders.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == orderId && (owner == null || x.UserId == owner), ct);
        if (order is null)
            return "PaymentNotFound";
        if (order.GatewayEnvironment != db.ActivePaymentEnvironment)
            return "PaymentEnvironmentMismatch";
        if (order.State == PaymentState.Paid)
            return "PaymentConfirmed";
        if (
            order.Gateway == "discount"
            && order.AmountIls == 0
            && order.DiscountPercent == 100
            && order.DiscountCodeId != null
        )
        {
            await membership.ConfirmAsync(
                orderId,
                actor,
                false,
                "Full discount",
                "Reserved discount",
                "discount:" + orderId,
                0,
                "ILS",
                ct
            );
            return "PaymentConfirmed";
        }
        if (order.LastCheckedAt > DateTimeOffset.UtcNow.AddSeconds(-30))
            return "PaymentCheckWait";
        var gateway = registry.ForVerification(order.Gateway);
        if (gateway is null)
            return "GatewayUnavailable";
        // Durable claim prevents repeated site/bot polling from hammering the provider.
        var now = DateTimeOffset.UtcNow;
        var claimed = await db
            .PaymentOrders.Where(x =>
                x.Id == orderId
                && (x.LastCheckedAt == null || x.LastCheckedAt <= now.AddSeconds(-30))
            )
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.LastCheckedAt, now), ct);
        if (claimed == 0)
            return "PaymentCheckWait";
        GatewayCheck result;
        try
        {
            result = await gateway.CheckAsync(order, ct);
        }
        catch (PaymentProviderException)
        {
            return "GatewayUnavailable";
        }
        if (result.Confirmed)
        {
            if (result.OrderId != order.Id)
                throw new InvalidOperationException("PaymentMismatch");
            await membership.ConfirmAsync(
                orderId,
                actor,
                false,
                "Provider API confirmation",
                "Provider API",
                result.Reference,
                result.Amount,
                result.Currency,
                ct
            );
            return "PaymentConfirmed";
        }
        await db
            .PaymentOrders.Where(x =>
                x.Id == orderId
                && x.State != PaymentState.Paid
                && x.State != PaymentState.ManuallyApproved
            )
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(
                        y => y.State,
                        result.State == PaymentState.Paid
                        || result.State == PaymentState.ManuallyApproved
                            ? PaymentState.Pending
                            : result.State
                    ),
                ct
            );
        return result.Inconclusive ? "PaymentUncertain" : "PaymentNotConfirmed";
    }
}

public sealed class PaymentGatewayRegistry(
    HiringDb db,
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    IEnumerable<IPaymentGateway> gateways
)
{
    // Closing new sales must not prevent confirming an existing charge.
    public IPaymentGateway? ForVerification(string key) =>
        gateways.SingleOrDefault(x => x.Key == key && x.Ready);

    public async Task<IPaymentGateway[]> AvailableAsync(CancellationToken ct)
    {
        var enabled = await db
            .PaymentGatewaySettings.AsNoTracking()
            .Where(x => x.Enabled)
            .Select(x => x.Key)
            .ToListAsync(ct);
        return gateways
            .Where(x =>
                x.Ready
                && enabled.Contains(x.Key)
                && bool.TryParse(
                    configuration["Payments:" + x.Key + ":Enabled"],
                    out var configured
                )
                && configured
            )
            .ToArray();
    }
}
