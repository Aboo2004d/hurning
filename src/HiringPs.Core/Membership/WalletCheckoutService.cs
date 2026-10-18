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
    IDataProtectionProvider protection
)
{
    private readonly IDataProtector protector = protection.CreateProtector(
        "HiringPs.JawwalPay.Sessions.v1"
    );

    public async Task<string> InitiateAsync(
        Guid id,
        string owner,
        string? phone,
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
            if (MembershipRules.Paid(order.State))
                return "PaymentConfirmed";
            var now = DateTimeOffset.UtcNow;
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
        }
        // No transport retry: the persisted row is the one initiation claim for this order.
        try
        {
            var result = await gateway.InitiateAsync(order, mobile, ct);
            var expires = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn);
            if (expires > order.ExpiresAt)
                expires = order.ExpiresAt;
            var protectedKey = protector.Protect(result.SessionKey);
            var keyHash = Convert.ToHexString(
                SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(result.SessionKey))
            );
            await db
                .WalletPaymentSessions.Where(x => x.OrderId == id)
                .ExecuteUpdateAsync(
                    update =>
                        update
                            .SetProperty(x => x.ProtectedSessionKey, protectedKey)
                            .SetProperty(x => x.SessionKeyHash, keyHash)
                            .SetProperty(x => x.ExpiresAt, expires),
                    CancellationToken.None
                );
            return "WalletOtpSent";
        }
        catch (WalletRequestRejectedException)
        {
            await db
                .WalletPaymentSessions.Where(x => x.OrderId == id)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(x => x.InitiationRejected, true),
                    CancellationToken.None
                );
            await MarkFailedAsync(id);
            return "WalletInitiateRejected";
        }
        catch (PaymentProviderException)
        {
            return "WalletInitiationUnknown";
        }
    }

    public async Task<string> ConfirmAsync(Guid id, string owner, string? otp, CancellationToken ct)
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
        }
        // From this point a debit may happen. Browser cancellation must not interrupt
        // receipt persistence. The provider client supplies its own 20-second timeout.
        WalletReceipt receipt;
        try
        {
            receipt = await gateway.ConfirmAsync(order, sessionKey, otp!, CancellationToken.None);
        }
        catch (WalletRequestRejectedException)
        {
            await db
                .WalletPaymentSessions.Where(x => x.OrderId == id)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(x => x.ConfirmationRejected, true),
                    CancellationToken.None
                );
            await MarkFailedAsync(id);
            return "WalletConfirmRejected";
        }
        catch (PaymentProviderException)
        {
            return "PaymentUncertain";
        }
        // A saved receipt lets the worker recover even after the transaction leaves
        // the partner's last-50 list. Its unique ID cannot fund two website orders.
        await gateway.SaveReceiptAsync(id, receipt, CancellationToken.None);
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
            )
            .ExecuteUpdateAsync(
                update => update.SetProperty(x => x.State, PaymentState.Failed),
                CancellationToken.None
            );
}
