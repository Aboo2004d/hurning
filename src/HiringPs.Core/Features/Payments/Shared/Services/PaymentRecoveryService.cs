using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يختار طلبات الدفع المحتاجة للتسوية دون إعادة إرسال أي أمر خصم.</summary>
public sealed class PaymentRecoveryService(HiringDb db)
{
    /// <summary>يعيد دفعة عادلة من الطلبات غير المؤكدة؛ يبطئ فحص القديم دون إسقاطه نهائيًا.</summary>
    public Task<List<Guid>> CandidatesAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var day = now.AddDays(-1);
        var week = now.AddDays(-7);
        var recentCheck = now.AddMinutes(-5);
        var hourlyCheck = now.AddHours(-1);
        var dailyCheck = now.AddDays(-1);
        return db.PaymentOrders.AsNoTracking().Where(x =>
            x.GatewayEnvironment == db.ActivePaymentEnvironment
            && x.State != PaymentState.Paid && x.State != PaymentState.Cancelled
            && ((x.Gateway == "Lahza" && x.CheckoutStartedAt != null && x.ProviderReference != null)
                || (x.Gateway == "JawwalPay" && db.WalletPaymentSessions.Any(session =>
                    session.OrderId == x.Id && session.ConfirmationStartedAt != null)))
            && (x.LastCheckedAt == null
                || (x.CreatedAt >= day && x.LastCheckedAt < recentCheck)
                || (x.CreatedAt < day && x.CreatedAt >= week && x.LastCheckedAt < hourlyCheck)
                || (x.CreatedAt < week && x.LastCheckedAt < dailyCheck)))
            .OrderBy(x => x.LastCheckedAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => x.Id).Take(5).ToListAsync(ct);
    }
}
