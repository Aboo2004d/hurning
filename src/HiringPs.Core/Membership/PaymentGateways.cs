using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

public sealed record GatewayCheck(
    bool Confirmed,
    Guid OrderId,
    string? Reference,
    decimal? Amount,
    string? Currency,
    PaymentState State,
    bool Inconclusive = false,
    string? DiagnosticCode = null
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
    PaymentGatewayRegistry registry,
    PaymentTraceWriter? diagnostics = null
)
{
    public async Task<string> CheckAsync(
        Guid orderId,
        string? owner,
        string actor,
        CancellationToken ct
    )
    {
        await using var trace = diagnostics?.Start(orderId);
        try
        {
            var order = await db
                .PaymentOrders.AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == orderId && (owner == null || x.UserId == owner),
                    ct
                );
            if (order is null)
            {
                trace?.Step("Order", "NotFound");
                return "PaymentNotFound";
            }
            if (trace is not null)
            {
                trace.OrderEnvironment = order.GatewayEnvironment;
                trace.RuntimeEnvironment = db.ActivePaymentEnvironment;
                trace.Gateway = order.Gateway;
            }
            trace?.Step("Order", order.State.ToString());
            if (order.GatewayEnvironment != db.ActivePaymentEnvironment)
            {
                trace?.Step("Environment", "OrderEnvironmentMismatch");
                return "PaymentEnvironmentMismatch";
            }
            if (order.State == PaymentState.Paid)
            {
                trace?.Step("Complete", "AlreadyPaid");
                if (trace is not null)
                    trace.Persist = false;
                return "PaymentConfirmed";
            }
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
            {
                trace?.Step("Throttle", "Wait30Seconds");
                if (trace is not null)
                    trace.Persist = false;
                return "PaymentCheckWait";
            }
            // Durable claim prevents repeated site/bot polling from hammering the provider.
            var now = DateTimeOffset.UtcNow;
            var claimed = await db
                .PaymentOrders.Where(x =>
                    x.Id == orderId
                    && (x.LastCheckedAt == null || x.LastCheckedAt <= now.AddSeconds(-30))
                )
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.LastCheckedAt, now), ct);
            if (claimed == 0)
            {
                trace?.Step("Throttle", "ConcurrentCheck");
                if (trace is not null)
                    trace.Persist = false;
                return "PaymentCheckWait";
            }
            var gateway = registry.ForVerification(order.Gateway);
            if (gateway is null)
            {
                var adapter = registry.FindAdapter(order.Gateway);
                if (trace is not null)
                    trace.AdapterEnvironment = adapter?.Environment;
                trace?.Step(
                    "Configuration",
                    adapter is LahzaGateway lahza ? lahza.ConfigurationStatus : "AdapterNotReady"
                );
                return "GatewayUnavailable";
            }
            if (trace is not null)
                trace.AdapterEnvironment = gateway.Environment;
            trace?.Step("Configuration", "Ready");
            GatewayCheck result;
            try
            {
                trace?.Step("Provider", "VerifyStarted");
                result = await gateway.CheckAsync(order, ct);
                trace?.Step(
                    "Provider",
                    result.DiagnosticCode
                        ?? (result.Confirmed ? "Confirmed" : result.State.ToString())
                );
            }
            catch (PaymentProviderException ex)
            {
                if (trace is not null)
                    trace.ReceiptFacts = ex.ReceiptFacts;
                trace?.Step("ProviderFailure", ex.ReasonCode, ex.HttpStatus);
                return "GatewayUnavailable";
            }
            if (result.Confirmed)
            {
                if (result.OrderId != order.Id)
                    throw new InvalidOperationException("PaymentMismatch");
                trace?.Step("Subscription", "SaveStarted");
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
                trace?.Step("Complete", "SubscriptionSaved");
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
            trace?.Step("Complete", "PaymentNotConfirmed");
            return result.Inconclusive ? "PaymentUncertain" : "PaymentNotConfirmed";
        }
        catch (Exception ex)
        {
            var sql =
                ex as Microsoft.Data.SqlClient.SqlException
                ?? ex.InnerException as Microsoft.Data.SqlClient.SqlException;
            trace?.Step("Failure", sql is null ? ex.GetType().Name : "SqlError" + sql.Number);
            throw;
        }
    }
}

public sealed class PaymentGatewayRegistry(
    HiringDb db,
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    IEnumerable<IPaymentGateway> gateways
)
{
    // Closing new sales must not prevent confirming an existing charge.
    public IPaymentGateway? FindAdapter(string key) => gateways.SingleOrDefault(x => x.Key == key);

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
