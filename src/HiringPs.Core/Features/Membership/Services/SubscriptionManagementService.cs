using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يدير إيقاف الاشتراك داخل معاملة وقفل العضو دون اعتماد على HTTP.</summary>
public sealed class SubscriptionManagementService(HiringDb db, MembershipService membership)
{
    /// <summary>يوقف الاشتراك ويسجل السبب بعد قفل العضو؛ لا يغير أي طلب دفع أو يسترده.</summary>
    public async Task<string> CancelAsync(long id, string reason, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1500)
            throw new ArgumentException("A cancellation reason of at most 1500 characters is required.", nameof(reason));
        var owner = await db
            .MemberSubscriptions.Where(x => x.Id == id)
            .Select(x => x.UserId)
            .SingleAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            ct
        );
        await membership.LockMemberAsync(owner, ct);
        var sub = await db.MemberSubscriptions.SingleAsync(x => x.Id == id, ct);
        sub.Cancelled = true;
        membership.Audit(
            actor,
            "Subscription.Stop",
            $"Member {owner}; subscription {id}; {reason}"
        );
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return owner;
    }
}
