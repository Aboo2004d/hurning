using System.Data;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قواعد استحقاق العضوية وتأكيد الدفع ومنح الاشتراك والإيقاف داخل معاملات القاعدة.</summary>
public sealed class MembershipService(HiringDb db)
{
    /// <summary>يحسب أنواع تفاصيل الفرص التي يحق للعضو قراءتها حاليًا.</summary>
    public async Task<HashSet<OpportunityKind>> DetailKindsAsync(
        string? userId,
        bool administrator,
        CancellationToken ct
    )
    {
        if (administrator)
            return Enum.GetValues<OpportunityKind>().ToHashSet();
        var kinds = (
            await db
                .OpportunityAccessRules.AsNoTracking()
                .Where(x => x.PublicDetails)
                .Select(x => x.Kind)
                .ToListAsync(ct)
        ).ToHashSet();
        if (userId is null)
            return kinds;
        var member = await db
            .MemberProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (member is not { Activated: true, Suspended: false })
            return kinds;
        var now = DateTimeOffset.UtcNow;
        var rights = await db
            .MemberSubscriptions.AsNoTracking()
            .Where(x => x.UserId == userId && !x.Cancelled && x.StartsAt <= now && x.EndsAt > now)
            .Select(x => x.PlanVersion.DetailKinds)
            .ToListAsync(ct);
        foreach (var kind in Enum.GetValues<OpportunityKind>())
            if (rights.Any(mask => MembershipRules.Includes(mask, kind)))
                kinds.Add(kind);
        return kinds;
    }

    /// <summary>ينشئ أثر تدقيق للعملية الإدارية دون اعتماد دفع من تلقاء نفسه.</summary>
    public void Audit(string actor, string action, string detail) =>
        db.Audit.Add(
            new AuditEntry
            {
                Actor = actor.Truncate(200),
                Action = action.Truncate(200),
                Detail = detail.Truncate(2000),
            }
        );

    // Lock one account to serialize payments, grants, suspension and renewals.
    /// <summary>يقفل سجل العضو داخل المعاملة لحماية تغييرات الاشتراك المتزامنة.</summary>
    public async Task<MemberProfile> LockMemberAsync(string userId, CancellationToken ct)
    {
        var member = await db
            .MemberProfiles.FromSqlInterpolated(
                $"SELECT * FROM [MemberProfiles] WITH (UPDLOCK, HOLDLOCK) WHERE [UserId] = {userId}"
            )
            .SingleAsync(ct);
        await db.Entry(member).ReloadAsync(ct);
        return member;
    }

    /// <summary>يعتمد دفعة مثبتة ويمنح الاشتراك مع قفل السجلات والتحقق من المرجع والمبلغ والعملة.</summary>
    public async Task ConfirmAsync(
        Guid orderId,
        string actor,
        bool manual,
        string reason,
        string evidence,
        string? reference,
        decimal? amount,
        string? currency,
        CancellationToken ct
    )
    {
        // Read ownership before the serializable transaction: competing confirmations
        // must not retain shared order locks while waiting for the account lock.
        var owner = await db
            .PaymentOrders.AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => x.UserId)
            .SingleAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct
        );
        var member = await LockMemberAsync(owner, ct);
        var order = await db
            .PaymentOrders.Include(x => x.PlanVersion)
            .SingleAsync(x => x.Id == orderId, ct);
        if (order.GatewayEnvironment != db.ActivePaymentEnvironment)
            throw new InvalidOperationException("PaymentEnvironmentMismatch");
        if (MembershipRules.Paid(order.State))
        {
            if (!manual && order.State == PaymentState.ManuallyApproved)
            {
                if (
                    amount != order.AmountIls
                    || currency != "ILS"
                    || string.IsNullOrWhiteSpace(reference)
                )
                    throw new InvalidOperationException("PaymentMismatch");
                order.State = PaymentState.Paid;
                order.ProviderReference = reference;
                Audit(
                    actor,
                    "Payment.VerifiedAfterManual",
                    $"Member {owner}; order {order.Id}; provider confirmation after manual approval; no additional period"
                );
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            return;
        }
        if (manual && (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(evidence)))
            throw new InvalidOperationException("ManualEvidenceRequired");
        if (
            !manual
            && (
                amount != order.AmountIls
                || currency != "ILS"
                || string.IsNullOrWhiteSpace(reference)
            )
        )
            throw new InvalidOperationException("PaymentMismatch");
        var now = DateTimeOffset.UtcNow;
        var existing = await db
            .MemberSubscriptions.Where(x => x.UserId == owner && !x.Cancelled)
            .ToListAsync(ct);
        // A suspended frozen account uses its frozen clock until it resumes.
        var clock = member.Suspended && member.FreezeSubscription ? member.SuspendedAt ?? now : now;
        var start = MembershipRules.Starts(clock, existing);
        db.MemberSubscriptions.Add(
            new MemberSubscription
            {
                UserId = owner,
                PlanVersionId = order.PlanVersionId,
                OrderId = order.Id,
                GatewayEnvironment = order.GatewayEnvironment,
                StartsAt = start,
                EndsAt = start.AddDays(order.PlanVersion.Days),
            }
        );
        order.State = manual ? PaymentState.ManuallyApproved : PaymentState.Paid;
        order.PaidAt = now;
        order.ProviderReference = reference ?? order.ProviderReference;
        order.DecisionReason = reason;
        order.PaymentEvidence = evidence;
        Audit(
            actor,
            manual ? "Payment.ManualApprove" : "Payment.Confirm",
            $"Member {owner}; order {order.Id}; {reason}; evidence: {evidence}"
        );
        var en = member.Language == "en";
        db.MemberMessages.Add(
            new MemberMessage
            {
                UserId = owner,
                EventKey = "payment:" + order.Id,
                Kind = MemberMessageKind.Payment,
                Text =
                    (en ? "Payment confirmed: " : "تم اعتماد الدفع: ")
                    + order.PlanVersion.Name(member.Language)
                    + " · "
                    + MembershipRules.Money(order.AmountIls)
                    + (en ? " · Starts (Palestine time): " : " · البداية (توقيت فلسطين): ")
                    + SourceSchedule.Display(start)
                    + (en ? " · Ends (Palestine time): " : " · النهاية (توقيت فلسطين): ")
                    + SourceSchedule.Display(start.AddDays(order.PlanVersion.Days)),
            }
        );
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>يمنح الاشتراك وفق مدة الباقة وبيانات الدفع المثبتة داخل المعاملة.</summary>
    public async Task GrantAsync(
        string userId,
        int versionId,
        int? extraDays,
        string actor,
        string reason,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("ReasonRequired");
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct
        );
        var member = await LockMemberAsync(userId, ct);
        var version = await db.MembershipPlanVersions.SingleAsync(x => x.Id == versionId, ct);
        var days = extraDays ?? version.Days;
        if (days is < 1 or > 3650)
            throw new InvalidOperationException("InvalidDays");
        var existing = await db
            .MemberSubscriptions.Where(x => x.UserId == userId && !x.Cancelled)
            .ToListAsync(ct);
        var clock =
            member.Suspended && member.FreezeSubscription
                ? member.SuspendedAt!.Value
                : DateTimeOffset.UtcNow;
        var start = MembershipRules.Starts(clock, existing);
        db.MemberSubscriptions.Add(
            new MemberSubscription
            {
                UserId = userId,
                PlanVersionId = versionId,
                GatewayEnvironment = db.ActivePaymentEnvironment,
                StartsAt = start,
                EndsAt = start.AddDays(days),
            }
        );
        Audit(
            actor,
            "Subscription.Grant",
            $"Member {userId}; version {versionId}; days {days}; {reason}"
        );
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>يغير إيقاف العضو وتجميد اشتراكه مع حفظ السبب وأثر التدقيق.</summary>
    public async Task SuspendAsync(
        string userId,
        bool suspend,
        bool freeze,
        string reason,
        string actor,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("ReasonRequired");
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct
        );
        var member = await LockMemberAsync(userId, ct);
        var now = DateTimeOffset.UtcNow;
        if (member.Suspended == suspend)
            return;
        if (!suspend && member.FreezeSubscription && member.SuspendedAt is { } from)
        {
            var offset = now - from;
            var subscriptions = await db
                .MemberSubscriptions.Where(x =>
                    x.UserId == userId && !x.Cancelled && x.EndsAt > from
                )
                .ToListAsync(ct);
            foreach (var sub in subscriptions)
            {
                if (sub.StartsAt >= from)
                    sub.StartsAt += offset;
                sub.EndsAt += offset;
            }
        }
        member.Suspended = suspend;
        member.SuspensionReason = reason.Trim();
        member.FreezeSubscription = suspend && freeze;
        member.SuspendedAt = suspend ? now : null;
        Audit(
            actor,
            suspend ? "Member.Suspend" : "Member.Resume",
            $"Member {userId}; freeze={freeze}; {reason}"
        );
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
