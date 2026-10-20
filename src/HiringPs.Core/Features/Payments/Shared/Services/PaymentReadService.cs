using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>استعلامات صفحات الدفع؛ تعيد إسقاطات للقراءة ولا تعيد كيانات EF للواجهات.</summary>
public sealed class PaymentReadService(HiringDb db)
{
    /// <summary>يفحص أن مفتاح المعاينة أصبح جلسة دفع مجمدة للعضو في البيئة الحالية دون تحميل الطلب.</summary>
    public Task<bool> HasMemberRequestAsync(Guid id, string owner, CancellationToken ct) =>
        db.PaymentOrders.AnyAsync(x => x.Id == id && x.UserId == owner
            && x.GatewayEnvironment == db.ActivePaymentEnvironment, ct);

    /// <summary>يفحص إعادة استخدام مفتاح شراء قبل إعادة التحقق من خصمه؛ لا يعيد بيانات مالك آخر.</summary>
    public async Task<PaymentRequestOwnership> RequestOwnershipAsync(Guid id, string owner, CancellationToken ct)
    {
        var request = await db.PaymentOrders.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.UserId, x.GatewayEnvironment }).SingleOrDefaultAsync(ct);
        return request is null ? PaymentRequestOwnership.Missing
            : request.UserId == owner && request.GatewayEnvironment == db.ActivePaymentEnvironment
                ? PaymentRequestOwnership.Owned : PaymentRequestOwnership.OtherOwnerOrEnvironment;
    }

    /// <summary>يعيد قائمة طلبات العضو في بيئة الدفع الحالية، مرتبة للترقيم المستقر.</summary>
    public IQueryable<PaymentOrderSummaryDto> MemberOrders(string owner) =>
        db.PaymentOrders.AsNoTracking().Where(x => x.UserId == owner && x.GatewayEnvironment == db.ActivePaymentEnvironment)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(PaymentOrderProjection.For(db));

    /// <summary>يطبق مرشحات الإدارة دون تغيير حدود بيئة البحث الحالية أو البحث بمعاملة المحفظة.</summary>
    public IQueryable<PaymentOrderSummaryDto> AdminOrders(PaymentSearchDto filter)
    {
        var query = db.PaymentOrders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = filter.Query;
            Guid.TryParse(q, out var orderId);
            long.TryParse(q, out var transactionId);
            query = query.Where(x => x.Id == orderId || x.ProviderReference == q || x.UserId == q
                || db.WalletPaymentSessions.Any(session => session.OrderId == x.Id
                    && (session.JawwalReference == q || session.TransactionId == transactionId)));
        }
        if (!string.IsNullOrWhiteSpace(filter.Gateway))
            query = query.Where(x => x.Gateway == filter.Gateway);
        if (filter.Environment is "Sandbox" or "Production")
            query = query.Where(x => x.GatewayEnvironment == filter.Environment);
        if (filter.State is not null)
            query = query.Where(x => x.State == filter.State);
        if (filter.Review)
            query = query.Where(x => db.PaymentReviews.Any(r => r.OrderId == x.Id && r.ResolvedAt == null));
        if (filter.Unresolved)
            query = query.Where(x =>
            x.State != PaymentState.Paid && x.State != PaymentState.ManuallyApproved && x.State != PaymentState.Cancelled
            && (x.CheckoutStartedAt != null || (x.Gateway == "JawwalPay" && db.WalletPaymentSessions.Any(session =>
                session.OrderId == x.Id && session.ConfirmationStartedAt != null))));
        return query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Select(PaymentOrderProjection.For(db));
    }

    /// <summary>يقرأ تفاصيل الطلب لصاحبه، أو للإدارة عند تفويضها وتمرير owner فارغًا.</summary>
    public Task<PaymentOrderSummaryDto?> OrderAsync(Guid id, string? owner, CancellationToken ct) =>
        db.PaymentOrders.AsNoTracking().Where(x => x.Id == id && (owner == null || x.UserId == owner))
            .Select(PaymentOrderProjection.For(db)).SingleOrDefaultAsync(ct);

    /// <summary>يقرأ مراجعة الطلب بعد التحقق من ملكيته، أو من صلاحية الإدارة في المستدعي.</summary>
    public Task<PaymentReviewSummaryDto?> ReviewAsync(Guid id, string? owner, CancellationToken ct) =>
        db.PaymentReviews.AsNoTracking().Where(x => x.OrderId == id && (owner == null || x.UserId == owner))
            .Select(x => new PaymentReviewSummaryDto(x.Note, x.Resolution, x.ResolvedAt, x.AttachmentName)).SingleOrDefaultAsync(ct);

    /// <summary>يقرأ آخر عشرة آثار تحقق للطلب؛ يجب تفويض الإدارة قبل الاستدعاء.</summary>
    public Task<List<PaymentTraceSummaryDto>> AdminTracesAsync(Guid id, CancellationToken ct)
    {
        var actor = "Payment:" + id.ToString("N");
        return db.Audit.AsNoTracking().Where(x => x.Action == "Payment.VerifyTrace" && x.Actor == actor)
            .OrderByDescending(x => x.At).ThenByDescending(x => x.Id).Take(10)
            .Select(x => new PaymentTraceSummaryDto { At = x.At, Detail = x.Detail }).ToListAsync(ct);
    }

    /// <summary>يقرأ الأرقام المالية لإيصال المحفظة للعرض الإداري فقط دون مفتاح الجلسة.</summary>
    public Task<WalletReceiptSummary?> WalletReceiptAsync(Guid id, CancellationToken ct) =>
        db.WalletPaymentSessions.AsNoTracking().Where(x => x.OrderId == id)
            .Select(x => new WalletReceiptSummary(x.TransactionId, x.JawwalReference, x.ConfirmedAmount,
                x.JawwalFee, x.PlatformFee, x.NetCredited)).SingleOrDefaultAsync(ct);

    /// <summary>يقرأ حالة جلسة المحفظة المرتبطة بطلب العضو ويعيد هاتفًا مقنعًا فقط.</summary>
    public async Task<WalletCheckoutSnapshotDto?> WalletAsync(Guid id, string owner, CancellationToken ct)
    {
        var session = await db.WalletPaymentSessions.AsNoTracking()
            .Where(x => x.OrderId == id && x.Order.UserId == owner)
            .Select(x => new
            {
                x.Phone,
                HasKey = x.ProtectedSessionKey != null,
                x.ExpiresAt,
                x.ConfirmationStartedAt,
                x.InitiationRejected,
                x.ConfirmationRejected
            }).SingleOrDefaultAsync(ct);
        if (session is null)
            return null;
        var suffix = session.Phone.Length >= 4 ? session.Phone[^4..] : "••••";
        return new(session.HasKey, session.ExpiresAt, session.ConfirmationStartedAt,
            session.InitiationRejected, session.ConfirmationRejected, "05••••" + suffix);
    }
}
