using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

public sealed class MemberNotifications(
    HiringDb db,
    TelegramClient telegram,
    IConfiguration configuration
)
{
    private async Task<bool> EntitledAsync(
        MemberProfile member,
        Opportunity item,
        DateTimeOffset eventAt,
        bool saved,
        CancellationToken ct
    )
    {
        var now = DateTimeOffset.UtcNow;
        if (
            !member.Activated
            || member.Suspended
            || !member.NotificationsEnabled
            || member.TelegramChatId is null
            || member.NotificationsSince > eventAt
            || (saved && !member.SavedRemindersEnabled)
        )
            return false;
        var rights = await db
            .MemberSubscriptions.Where(x =>
                x.UserId == member.UserId
                && !x.Cancelled
                && x.StartsAt <= now
                && x.StartsAt <= eventAt
                && x.EndsAt > now
            )
            .Select(x => x.PlanVersion.AlertKinds)
            .ToListAsync(ct);
        return rights.Any(mask => MembershipRules.Includes(mask, item.Kind));
    }

    private string Link(MemberProfile member, long id) =>
        (configuration["Seo:PublicOrigin"] ?? "https://hiring.ps").TrimEnd('/')
        + "/"
        + member.Language
        + "/Opportunities/Details/"
        + id;

    public async Task ExpandPublicationsAsync(CancellationToken ct)
    {
        var publication = await db
            .OpportunityPublications.Include(x => x.Opportunity)
            .Where(x => !x.Completed)
            .OrderBy(x => x.PublishedAt)
            .FirstOrDefaultAsync(ct);
        if (publication is null)
            return;
        var item = publication.Opportunity;
        if (
            item.Status != PublicationStatus.Published
            || item.Deadline < TextRules.PalestineToday()
        )
        {
            publication.Completed = true;
            await db.SaveChangesAsync(ct);
            return;
        }
        var members = await db
            .MemberProfiles.Where(x =>
                x.Activated
                && !x.Suspended
                && x.NotificationsEnabled
                && x.CreatedAt <= publication.PublishedAt
                && (
                    publication.AfterUserId == null
                    || string.Compare(x.UserId, publication.AfterUserId) > 0
                )
            )
            .OrderBy(x => x.UserId)
            .Take(100)
            .ToListAsync(ct);
        foreach (var member in members)
        {
            var pref = await db.MemberPreferences.SingleAsync(x => x.UserId == member.UserId, ct);
            if (
                pref.ChangedAt <= publication.PublishedAt
                && MembershipRules.Matches(pref, item)
                && await EntitledAsync(member, item, publication.PublishedAt, false, ct)
            )
                await EnqueueAsync(
                    member,
                    "opportunity:" + item.Id,
                    MemberMessageKind.Opportunity,
                    item.Title + "\n" + Link(member, item.Id),
                    item.Id,
                    publication.PublishedAt,
                    ct
                );
        }
        publication.AfterUserId = members.LastOrDefault()?.UserId ?? publication.AfterUserId;
        publication.Completed = members.Count < 100;
        await db.SaveChangesAsync(ct);
    }

    private async Task EnqueueAsync(
        MemberProfile member,
        string key,
        MemberMessageKind kind,
        string text,
        long? opportunityId,
        DateTimeOffset? eventAt,
        CancellationToken ct
    )
    {
        if (
            await db.MemberMessages.AnyAsync(
                x => x.UserId == member.UserId && x.EventKey == key,
                ct
            )
        )
            return;
        db.MemberMessages.Add(
            new MemberMessage
            {
                UserId = member.UserId,
                EventKey = key,
                Kind = kind,
                Text = text,
                OpportunityId = opportunityId,
                EventAt = eventAt,
            }
        );
    }

    public async Task RemindersAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        // Bounded query; subsequent ticks skip already enqueued event keys.
        var runtime = await db.MembershipRuntimeStates.SingleAsync(x => x.Id == 1, ct);
        var subscriptions = await db
            .MemberSubscriptions.AsNoTracking()
            .Include(x => x.PlanVersion)
            .Where(x =>
                x.Id > runtime.ReminderAfterSubscription
                && !x.Cancelled
                && x.EndsAt > now.AddDays(-1)
                && x.EndsAt <= now.AddDays(3)
            )
            .OrderBy(x => x.Id)
            .Take(100)
            .ToListAsync(ct);
        foreach (var sub in subscriptions)
        {
            // No expiration reminder at the boundary of consecutive paid periods.
            if (
                await db.MemberSubscriptions.AnyAsync(
                    x =>
                        x.UserId == sub.UserId
                        && !x.Cancelled
                        && x.Id != sub.Id
                        && x.StartsAt <= sub.EndsAt
                        && x.EndsAt > sub.EndsAt,
                    ct
                )
            )
                continue;
            var member = await db.MemberProfiles.SingleAsync(x => x.UserId == sub.UserId, ct);
            if (!member.Activated || member.Suspended)
                continue;
            var expired = sub.EndsAt <= now;
            var en = member.Language == "en";
            var key =
                "subscription:"
                + sub.Id
                + ":"
                + sub.EndsAt.UtcTicks
                + (expired ? ":ended" : ":soon");
            await EnqueueAsync(
                member,
                key,
                MemberMessageKind.Subscription,
                (
                    expired
                        ? en
                            ? "Your subscription has ended. "
                            : "انتهى اشتراكك. "
                        : en
                            ? "Your subscription ends within 3 days. "
                            : "ينتهي اشتراكك خلال 3 أيام. "
                )
                    + sub.PlanVersion.Name(member.Language)
                    + "\n"
                    + (configuration["Seo:PublicOrigin"] ?? "https://hiring.ps").TrimEnd('/')
                    + "/"
                    + member.Language
                    + "/Member/Plans",
                null,
                sub.EndsAt,
                ct
            );
        }
        runtime.ReminderAfterSubscription = subscriptions.Count < 100 ? 0 : subscriptions[^1].Id;
        var today = TextRules.PalestineToday();
        var tomorrow = today.AddDays(1);
        var saved = await db
            .SavedOpportunities.Include(x => x.Opportunity)
            .Where(x =>
                x.Id > runtime.ReminderAfterSaved
                && x.Opportunity.Status == PublicationStatus.Published
                && x.Opportunity.Deadline == tomorrow
            )
            .OrderBy(x => x.Id)
            .Take(100)
            .ToListAsync(ct);
        foreach (var save in saved)
        {
            var member = await db.MemberProfiles.SingleAsync(x => x.UserId == save.UserId, ct);
            if (!await EntitledAsync(member, save.Opportunity, now, true, ct))
                continue;
            await EnqueueAsync(
                member,
                "saved:"
                    + save.OpportunityId
                    + ":"
                    + tomorrow.ToString(
                        "yyyyMMdd",
                        System.Globalization.CultureInfo.InvariantCulture
                    ),
                MemberMessageKind.SavedDeadline,
                (
                    member.Language == "en"
                        ? "Application deadline is tomorrow: "
                        : "ينتهي التقديم غدًا: "
                )
                    + save.Opportunity.Title
                    + "\n"
                    + Link(member, save.OpportunityId),
                save.OpportunityId,
                now,
                ct
            );
        }
        runtime.ReminderAfterSaved = saved.Count < 100 ? 0 : saved[^1].Id;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeliverAsync(CancellationToken ct)
    {
        if (!telegram.Ready)
            return;
        var now = DateTimeOffset.UtcNow;
        var ids = await db
            .MemberMessages.AsNoTracking()
            .Where(x =>
                x.SentAt == null
                && x.CancelledAt == null
                && x.AvailableAt <= now
                && (x.LockedUntil == null || x.LockedUntil < now)
            )
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Take(5)
            .ToListAsync(ct);
        foreach (var id in ids)
        {
            if (
                await db
                    .MemberMessages.Where(x =>
                        x.Id == id
                        && x.SentAt == null
                        && x.CancelledAt == null
                        && (x.LockedUntil == null || x.LockedUntil < now)
                    )
                    .ExecuteUpdateAsync(
                        x => x.SetProperty(y => y.LockedUntil, now.AddMinutes(2)),
                        ct
                    ) == 0
            )
                continue;
            var message = await db.MemberMessages.SingleAsync(x => x.Id == id, ct);
            // The atomic claim changes SQL rowversion, including messages queued in this scope.
            await db.Entry(message).ReloadAsync(ct);
            var member = await db.MemberProfiles.SingleAsync(x => x.UserId == message.UserId, ct);
            message.Attempts++;
            if (member.TelegramChatId is null)
            {
                message.LastError = "TelegramNotLinked";
                message.AvailableAt = now.AddHours(1);
                await db.SaveChangesAsync(ct);
                continue;
            }
            if (message.Kind is MemberMessageKind.Opportunity or MemberMessageKind.SavedDeadline)
            {
                var item = await db.Opportunities.SingleOrDefaultAsync(
                    x => x.Id == message.OpportunityId,
                    ct
                );
                var valid =
                    item is not null
                    && item.Status == PublicationStatus.Published
                    && (item.Deadline == null || item.Deadline >= TextRules.PalestineToday())
                    && await EntitledAsync(
                        member,
                        item,
                        message.EventAt ?? now,
                        message.Kind == MemberMessageKind.SavedDeadline,
                        ct
                    );
                if (valid && message.Kind == MemberMessageKind.Opportunity)
                {
                    var pref = await db.MemberPreferences.SingleAsync(
                        x => x.UserId == member.UserId,
                        ct
                    );
                    valid =
                        pref.ChangedAt <= message.EventAt && MembershipRules.Matches(pref, item!);
                }
                if (!valid)
                {
                    message.CancelledAt = now;
                    await db.SaveChangesAsync(ct);
                    continue;
                }
            }
            if (message.Kind == MemberMessageKind.Subscription)
            {
                var parts = message.EventKey.Split(':');
                var sub =
                    parts.Length >= 4 && long.TryParse(parts[1], out var subId)
                        ? await db.MemberSubscriptions.SingleOrDefaultAsync(x => x.Id == subId, ct)
                        : null;
                var valid =
                    sub is not null
                    && !sub.Cancelled
                    && !member.Suspended
                    && sub.EndsAt == message.EventAt
                    && !(
                        message.EventKey.EndsWith(":soon", StringComparison.Ordinal)
                        && sub.EndsAt <= now
                    )
                    && !await db.MemberSubscriptions.AnyAsync(
                        x =>
                            x.UserId == member.UserId
                            && !x.Cancelled
                            && x.Id != sub.Id
                            && x.StartsAt <= sub.EndsAt
                            && x.EndsAt > sub.EndsAt,
                        ct
                    );
                if (!valid)
                {
                    message.CancelledAt = now;
                    await db.SaveChangesAsync(ct);
                    continue;
                }
                var plan = await db.MembershipPlanVersions.SingleAsync(
                    x => x.Id == sub!.PlanVersionId,
                    ct
                );
                var english = member.Language == "en";
                message.Text =
                    (
                        sub!.EndsAt <= now
                            ? english
                                ? "Your subscription has ended. "
                                : "انتهى اشتراكك. "
                            : english
                                ? "Your subscription ends within 3 days. "
                                : "ينتهي اشتراكك خلال 3 أيام. "
                    )
                    + plan.Name(member.Language)
                    + "\n"
                    + (configuration["Seo:PublicOrigin"] ?? "https://hiring.ps").TrimEnd('/')
                    + "/"
                    + member.Language
                    + "/Member/Plans";
            }
            // Build language-dependent text at delivery, not when the queue was created.
            var en = member.Language == "en";
            if (message.OpportunityId is { } opportunityId)
            {
                var item = await db.Opportunities.SingleAsync(x => x.Id == opportunityId, ct);
                message.Text =
                    (
                        message.Kind == MemberMessageKind.SavedDeadline
                            ? en
                                ? "Application deadline is tomorrow: "
                                : "ينتهي التقديم غدًا: "
                            : ""
                    )
                    + item.Title
                    + "\n"
                    + Link(member, item.Id);
            }
            else if (
                message.Kind == MemberMessageKind.Payment
                && Guid.TryParse(message.EventKey["payment:".Length..], out var orderId)
            )
            {
                var order = await db
                    .PaymentOrders.Include(x => x.PlanVersion)
                    .SingleAsync(x => x.Id == orderId, ct);
                var subscription = await db.MemberSubscriptions.SingleAsync(
                    x => x.OrderId == orderId,
                    ct
                );
                message.Text =
                    (en ? "Payment confirmed: " : "تم اعتماد الدفع: ")
                    + order.PlanVersion.Name(member.Language)
                    + " · "
                    + MembershipRules.Money(order.AmountIls)
                    + (en ? " · Starts (UTC): " : " · البداية (UTC): ")
                    + subscription.StartsAt.ToString(
                        "yyyy-MM-dd HH:mm",
                        System.Globalization.CultureInfo.InvariantCulture
                    )
                    + (en ? " · Ends (UTC): " : " · النهاية (UTC): ")
                    + subscription.EndsAt.ToString(
                        "yyyy-MM-dd HH:mm",
                        System.Globalization.CultureInfo.InvariantCulture
                    );
            }
            else if (
                message.Kind == MemberMessageKind.Review
                && long.TryParse(message.EventKey["review:".Length..], out var reviewId)
            )
            {
                var review = await db.PaymentReviews.SingleAsync(x => x.Id == reviewId, ct);
                message.Text =
                    (en ? "Payment review completed. Order: " : "تمت معالجة مراجعة الدفع. الطلب: ")
                    + review.OrderId
                    + "\n"
                    + review.Resolution;
            }
            try
            {
                await telegram.SendAsync(member.TelegramChatId.Value, message.Text, ct);
                message.SentAt = DateTimeOffset.UtcNow;
                message.LastError = "";
            }
            catch (TelegramDeliveryException ex)
            {
                message.LastError = ex.Status == 403 ? "TelegramBlocked" : "TelegramDeliveryFailed";
                message.AvailableAt = now.AddMinutes(
                    Math.Min(1440, Math.Pow(2, Math.Min(10, message.Attempts)))
                );
            }
            catch (HttpRequestException)
            {
                message.LastError = "TelegramNetworkFailed";
                message.AvailableAt = now.AddMinutes(5);
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
