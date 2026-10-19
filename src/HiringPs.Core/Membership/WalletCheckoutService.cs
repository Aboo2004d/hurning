using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

public sealed class WalletCheckoutService(
    HiringDb db,
    MembershipService membership,
    PaymentGatewayRegistry registry,
    JawwalPayGateway gateway,
    IDataProtectionProvider protection,
    PaymentTraceWriter? diagnostics = null
)
{
    private readonly IDataProtector protector = protection.CreateProtector(
        "HiringPs.JawwalPay.Sessions.v1"
    );

    public Task<string> InitiateAsync(Guid id, string owner, string? phone, CancellationToken ct) =>
        TraceAsync(id, "WalletInitiate", trace => InitiateCoreAsync(id, owner, phone, trace, ct));

    public Task<string> ConfirmAsync(Guid id, string owner, string? otp, CancellationToken ct) =>
        TraceAsync(id, "WalletConfirm", trace => ConfirmCoreAsync(id, owner, otp, trace, ct));

    // Cancel locally before any debit claim. The supplied provider contract has no cancel endpoint.
    public async Task<string> CancelAsync(Guid id, string owner, CancellationToken ct)
    {
        if (!await OwnedAsync(id, owner, ct))
            return "PaymentNotFound";
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct
        );
        await membership.LockMemberAsync(owner, ct);
        var order = await db.PaymentOrders.SingleAsync(x => x.Id == id, ct);
        if (MembershipRules.Paid(order.State))
            return "PaymentConfirmed";
        var session = await db.WalletPaymentSessions.SingleOrDefaultAsync(x => x.OrderId == id, ct);
        if (session?.ConfirmationStartedAt is not null && !session.ConfirmationRejected)
            return "WalletCancellationUnavailable";
        var now = DateTimeOffset.UtcNow;
        order.State = PaymentState.Cancelled;
        order.ExpiresAt = now;
        if (session is not null)
        {
            session.ProtectedSessionKey = null;
            session.ExpiresAt = now;
        }
        membership.Audit(owner, "Wallet.Cancel", "Order " + id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return "WalletCancelled";
    }

    private async Task<string> TraceAsync(
        Guid id,
        string operation,
        Func<PaymentTraceSession?, Task<string>> run
    )
    {
        await using var trace = diagnostics?.Start(id);
        if (trace is not null)
        {
            trace.Gateway = "JawwalPay";
            trace.RuntimeEnvironment = db.ActivePaymentEnvironment;
            trace.AdapterEnvironment = gateway.Environment;
        }
        trace?.Step(operation, "RequestStarted");
        try
        {
            var outcome = await run(trace);
            trace?.Step("Complete", outcome);
            return outcome;
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

    private async Task<string> InitiateCoreAsync(
        Guid id,
        string owner,
        string? phone,
        PaymentTraceSession? trace,
        CancellationToken ct
    )
    {
        // Check ownership outside the serializable member lock to avoid retained shared locks.
        if (!await OwnedAsync(id, owner, ct))
            return "PaymentNotFound";
        string mobile;
        try
        {
            mobile = JawwalPayRules.Phone(phone);
        }
        catch (InvalidOperationException)
        {
            return "WalletMobileInvalid";
        }
        if (!(await registry.AvailableAsync(ct)).Any(x => x.Key == "JawwalPay"))
            return "GatewayUnavailable";
        PaymentOrder order;
        await using (
            var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct
            )
        )
        {
            var member = await membership.LockMemberAsync(owner, ct);
            if (!member.Activated || member.Suspended)
                return "AccountSuspended";
            order = await db.PaymentOrders.AsNoTracking().SingleAsync(x => x.Id == id, ct);
            if (trace is not null)
                trace.OrderEnvironment = order.GatewayEnvironment;
            if (MembershipRules.Paid(order.State))
                return "PaymentConfirmed";
            var now = DateTimeOffset.UtcNow;
            if (order.State == PaymentState.Cancelled)
                return "WalletCancelled";
            if (order.ExpiresAt <= now)
                return "PaymentExpired";
            if (order.Currency != "ILS" || !gateway.SupportsAmount(order.AmountIls))
                return "WalletAmountInvalid";
            if (await db.WalletPaymentSessions.AnyAsync(x => x.OrderId == id, ct))
                return "WalletAlreadyRequested";
            if (
                await db.WalletPaymentSessions.AnyAsync(
                    x => x.Order.UserId == owner && x.CreatedAt > now.AddMinutes(-1),
                    ct
                )
                || await db.WalletPaymentSessions.CountAsync(
                    x => x.Order.UserId == owner && x.CreatedAt > now.AddMinutes(-15),
                    ct
                ) >= 3
            )
                return "WalletRequestWait";
            db.WalletPaymentSessions.Add(
                new WalletPaymentSession
                {
                    OrderId = id,
                    GatewayEnvironment = order.GatewayEnvironment,
                    Phone = mobile,
                    CreatedAt = now,
                    ExpiresAt = order.ExpiresAt,
                }
            );
            await db
                .PaymentOrders.Where(x => x.Id == id)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(x => x.State, PaymentState.Pending),
                    ct
                );
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            trace?.Step("Session", "ClaimCommitted");
        }
        // No transport retry: the persisted row is the one initiation claim for this order.
        try
        {
            trace?.Step("ProviderInitiate", "RequestSent");
            var result = await gateway.InitiateAsync(order, mobile, ct);
            trace?.Step("ProviderInitiate", "ResponseParsed");
            var expires = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn);
            if (expires > order.ExpiresAt)
                expires = order.ExpiresAt;
            var protectedKey = protector.Protect(result.SessionKey);
            var keyHash = Convert.ToHexString(
                SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(result.SessionKey))
            );
            var saved = await db
                .WalletPaymentSessions.Where(x =>
                    x.OrderId == id
                    && x.Order.State != PaymentState.Cancelled
                    && x.ExpiresAt > DateTimeOffset.UtcNow
                )
                .ExecuteUpdateAsync(
                    update =>
                        update
                            .SetProperty(x => x.ProtectedSessionKey, protectedKey)
                            .SetProperty(x => x.SessionKeyHash, keyHash)
                            .SetProperty(x => x.ExpiresAt, expires),
                    CancellationToken.None
                );
            if (saved == 0)
                return await db.PaymentOrders.AnyAsync(
                    x => x.Id == id && x.State == PaymentState.Cancelled,
                    CancellationToken.None
                )
                    ? "WalletCancelled"
                    : "PaymentExpired";
            trace?.Step("Session", "Saved");
            return "WalletOtpSent";
        }
        catch (WalletRequestRejectedException)
        {
            trace?.Step("ProviderFailure", "WalletExplicitRejection", 422);
            await db
                .WalletPaymentSessions.Where(x => x.OrderId == id)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(x => x.InitiationRejected, true),
                    CancellationToken.None
                );
            await MarkFailedAsync(id);
            return "WalletInitiateRejected";
        }
        catch (PaymentProviderException ex)
        {
            if (trace is not null)
                trace.ReceiptFacts = ex.ReceiptFacts;
            trace?.Step("ProviderFailure", ex.ReasonCode, ex.HttpStatus);
            return "WalletInitiationUnknown";
        }
    }

    private async Task<string> ConfirmCoreAsync(
        Guid id,
        string owner,
        string? otp,
        PaymentTraceSession? trace,
        CancellationToken ct
    )
    {
        if (!await OwnedAsync(id, owner, ct))
            return "PaymentNotFound";
        if (!JawwalPayRules.ValidOtp(otp))
            return "WalletOtpInvalid";
        if (!gateway.Ready)
            return "GatewayUnavailable";
        PaymentOrder order;
        string sessionKey;
        await using (
            var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct
            )
        )
        {
            var member = await membership.LockMemberAsync(owner, ct);
            if (!member.Activated || member.Suspended)
                return "AccountSuspended";
            order = await db.PaymentOrders.AsNoTracking().SingleAsync(x => x.Id == id, ct);
            if (trace is not null)
                trace.OrderEnvironment = order.GatewayEnvironment;
            if (MembershipRules.Paid(order.State))
                return "PaymentConfirmed";
            var session = await db
                .WalletPaymentSessions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OrderId == id, ct);
            if (session?.ConfirmationStartedAt != null)
                return "WalletAlreadyConfirming";
            if (session?.ProtectedSessionKey is null || session.InitiationRejected)
                return "WalletSessionUnavailable";
            var now = DateTimeOffset.UtcNow;
            if (session.ExpiresAt <= now || order.ExpiresAt <= now)
                return "PaymentExpired";
            try
            {
                sessionKey = protector.Unprotect(session.ProtectedSessionKey);
            }
            catch (CryptographicException)
            {
                trace?.Step("Session", "SessionKeyUnprotectFailed");
                return "WalletSessionUnavailable";
            }
            var claimed = await db
                .WalletPaymentSessions.Where(x =>
                    x.OrderId == id && x.ConfirmationStartedAt == null
                )
                .ExecuteUpdateAsync(
                    update =>
                        update
                            .SetProperty(x => x.ConfirmationStartedAt, now)
                            .SetProperty(x => x.ProtectedSessionKey, (string?)null),
                    ct
                );
            if (claimed != 1)
                return "WalletAlreadyConfirming";
            await transaction.CommitAsync(ct);
            trace?.Step("Session", "ClaimCommitted");
        }
        // From this point a debit may happen. Browser cancellation must not interrupt
        // receipt persistence. The provider client supplies its own 20-second timeout.
        WalletReceipt receipt;
        try
        {
            trace?.Step("ProviderConfirm", "RequestSent");
            receipt = await gateway.ConfirmAsync(order, sessionKey, otp!, CancellationToken.None);
            if (trace is not null)
                trace.ReceiptFacts = new WalletReceiptValidationFacts(
                    receipt.TransactionId,
                    order.AmountIls,
                    receipt.Amount,
                    receipt.JawwalFee,
                    receipt.PlatformFee,
                    receipt.NetCredited
                );
            trace?.Step("ProviderConfirm", "ResponseParsed");
            if (receipt.IsSandboxSimulation)
                trace?.Step("Sandbox", "ZeroNetSimulation");
        }
        catch (WalletRequestRejectedException)
        {
            trace?.Step("ProviderFailure", "WalletExplicitRejection", 422);
            await db
                .WalletPaymentSessions.Where(x => x.OrderId == id)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(x => x.ConfirmationRejected, true),
                    CancellationToken.None
                );
            await MarkFailedAsync(id);
            return "WalletConfirmRejected";
        }
        catch (PaymentProviderException ex)
        {
            if (trace is not null)
                trace.ReceiptFacts = ex.ReceiptFacts;
            trace?.Step("ProviderFailure", ex.ReasonCode, ex.HttpStatus);
            return "PaymentUncertain";
        }
        // A saved receipt lets the worker recover even after the transaction leaves
        // the partner's last-50 list. Its unique ID cannot fund two website orders.
        trace?.Step("Receipt", "SaveStarted");
        await gateway.SaveReceiptAsync(id, receipt, CancellationToken.None);
        trace?.Step("Receipt", "Saved");
        trace?.Step("Subscription", "SaveStarted");
        await membership.ConfirmAsync(
            id,
            owner,
            false,
            "Jawwal Pay confirmation",
            "Partner transaction " + receipt.TransactionId,
            "portal:" + receipt.TransactionId.ToString(CultureInfo.InvariantCulture),
            receipt.Amount,
            "ILS",
            CancellationToken.None
        );
        return "PaymentConfirmed";
    }

    private Task<bool> OwnedAsync(Guid id, string owner, CancellationToken ct) =>
        db.PaymentOrders.AnyAsync(
            x =>
                x.Id == id
                && x.UserId == owner
                && x.Gateway == "JawwalPay"
                && x.GatewayEnvironment == db.ActivePaymentEnvironment,
            ct
        );

    private Task<int> MarkFailedAsync(Guid id) =>
        db
            .PaymentOrders.Where(x =>
                x.Id == id
                && x.State != PaymentState.Paid
                && x.State != PaymentState.ManuallyApproved
                && x.State != PaymentState.Cancelled
            )
            .ExecuteUpdateAsync(
                update => update.SetProperty(x => x.State, PaymentState.Failed),
                CancellationToken.None
            );
}
