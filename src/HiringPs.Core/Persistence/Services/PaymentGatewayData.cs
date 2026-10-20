using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>تنفيذ EF لبيانات محولات الدفع؛ لا يتصل بالمزود ولا يعتمد اشتراكًا.</summary>
public sealed class PaymentGatewayData(HiringDb db) : IPaymentGatewayData
{
    /// <inheritdoc />
    public async Task<PaymentBuyerDto> BuyerAsync(string userId, CancellationToken ct)
    {
        var email = await db.Users.Where(x => x.Id == userId).Select(x => x.Email).SingleAsync(ct);
        var language = await db.MemberProfiles.Where(x => x.UserId == userId).Select(x => x.Language).SingleAsync(ct);
        return new(email, language);
    }

    /// <inheritdoc />
    public Task<WalletVerificationDto?> WalletAsync(Guid orderId, string environment, CancellationToken ct) =>
        db.WalletPaymentSessions.AsNoTracking()
            .Where(x => x.OrderId == orderId && x.GatewayEnvironment == environment)
            .Select(x => new WalletVerificationDto(x.Phone, x.TransactionId, x.ConfirmedAmount,
                x.ConfirmationStartedAt, x.ConfirmationRejected))
            .SingleOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task SaveWalletReceiptAsync(Guid orderId, string environment, WalletReceipt receipt, CancellationToken ct)
    {
        var changed = await db.WalletPaymentSessions.Where(x =>
            x.OrderId == orderId && x.GatewayEnvironment == environment
            && (x.TransactionId == null || x.TransactionId == receipt.TransactionId))
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.TransactionId, receipt.TransactionId)
                .SetProperty(x => x.JawwalReference, receipt.Reference)
                .SetProperty(x => x.ConfirmedAmount, receipt.Amount)
                .SetProperty(x => x.JawwalFee, receipt.JawwalFee)
                .SetProperty(x => x.PlatformFee, receipt.PlatformFee)
                .SetProperty(x => x.NetCredited, receipt.NetCredited)
                .SetProperty(x => x.ConfirmedAt, DateTimeOffset.UtcNow), ct);
        if (changed != 1) throw new PaymentProviderException();
    }
}
