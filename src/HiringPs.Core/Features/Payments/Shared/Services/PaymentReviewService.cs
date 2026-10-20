using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يدير طلب مراجعة الدفع والبت الإداري فيه وصلاحية الوصول للمرفق دون الاعتماد على MVC.</summary>
public sealed class PaymentReviewService(HiringDb db, MembershipService membership)
{
    /// <summary>يتحقق من ملكية الطلب وإتاحة البلاغ قبل حفظ المرفق والمراجعة؛ لا يعتمد دفعة.</summary>
    public async Task<PaymentReviewResult> RequestAsync(PaymentReviewRequestDto request,
        Func<CancellationToken, Task<string?>> saveEvidence, CancellationToken ct)
    {
        var (id, owner, note, returned) = request;
        var order = await db.PaymentOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
        if (order is null) return new(PaymentReviewOutcome.NotFound);
        var session = await db.WalletPaymentSessions.AsNoTracking().Where(x => x.OrderId == id)
            .Select(x => new { x.ConfirmationStartedAt, x.ConfirmationRejected }).SingleOrDefaultAsync(ct);
        var options = PaymentActionRules.For(new(order.State, order.Gateway, order.ExpiresAt,
            order.CheckoutStartedAt, order.LastCheckedAt), false, session?.ConfirmationStartedAt is not null,
            session?.ConfirmationRejected == true, returned);
        if (!options.CanReport || note?.Length > 2000) return new(PaymentReviewOutcome.InvalidRequest);
        if (await db.PaymentReviews.AnyAsync(x => x.OrderId == id, ct)) return new(PaymentReviewOutcome.AlreadyExists);
        try
        {
            var name = await saveEvidence(ct);
            db.PaymentReviews.Add(new PaymentReview
            {
                OrderId = id, UserId = owner, Note = note?.Trim() ?? "", AttachmentName = name
            });
            await db.SaveChangesAsync(ct);
            return new(PaymentReviewOutcome.Completed, "ReviewRequested");
        }
        catch (InvalidOperationException ex)
        {
            return new(PaymentReviewOutcome.Rejected, ex.Message);
        }
    }

    /// <summary>يعتمد الدفع يدويًا بسبب ودليل إلزاميين، وضمن بيئة التشغيل الحالية فقط.</summary>
    public async Task<PaymentReviewResult> ApproveAsync(ManualPaymentApprovalDto command, CancellationToken ct)
    {
        var (id, actor, reason, evidence) = command;
        if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(evidence)
            || reason.Length > 1500 || evidence.Length > 1500) return new(PaymentReviewOutcome.InvalidRequest);
        if (!await db.PaymentOrders.AnyAsync(x => x.Id == id && x.GatewayEnvironment == db.ActivePaymentEnvironment, ct))
            return new(PaymentReviewOutcome.EnvironmentMismatch, "PaymentEnvironmentMismatch");
        await membership.ConfirmAsync(id, actor, true, reason, evidence, null, null, null, ct);
        return new(PaymentReviewOutcome.Completed);
    }

    /// <summary>يسجل قرار مراجعة واحدًا وينشئ إشعار العضو وأثر التدقيق في عملية الحفظ نفسها.</summary>
    public async Task<PaymentReviewResult> ResolveAsync(PaymentReviewResolutionDto command, CancellationToken ct)
    {
        var (id, actor, result) = command;
        if (string.IsNullOrWhiteSpace(result) || result.Length > 1500) return new(PaymentReviewOutcome.InvalidRequest);
        var review = await db.PaymentReviews.SingleOrDefaultAsync(x => x.OrderId == id, ct);
        if (review is null) return new(PaymentReviewOutcome.NotFound);
        if (review.ResolvedAt is null)
        {
            review.ResolvedAt = DateTimeOffset.UtcNow;
            review.Resolution = result.Trim();
            var member = await db.MemberProfiles.SingleAsync(x => x.UserId == review.UserId, ct);
            db.MemberMessages.Add(new MemberMessage
            {
                UserId = review.UserId, EventKey = "review:" + review.Id, Kind = MemberMessageKind.Review,
                Text = (member.Language == "en" ? "Payment review completed. Order: " : "تمت معالجة مراجعة الدفع. الطلب: ")
                    + id + "\n" + result
            });
            membership.Audit(actor, "Payment.ReviewResolve", $"Member {review.UserId}; order {id}; {result}");
            await db.SaveChangesAsync(ct);
        }
        return new(PaymentReviewOutcome.Completed);
    }

    /// <summary>يعيد اسم المرفق المسموح لصاحب الطلب؛ تمرر الإدارة owner فارغًا بعد تفويض دورها في الواجهة.</summary>
    public Task<string?> EvidenceNameAsync(Guid id, string? owner, CancellationToken ct) =>
        db.PaymentReviews.AsNoTracking().Where(x => x.OrderId == id && (owner == null || x.UserId == owner))
            .Select(x => x.AttachmentName).SingleOrDefaultAsync(ct);
}
