using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>Reads notification queues and schedules explicit retries without sending a message itself.</summary>
public sealed class NotificationAdministrationService(HiringDb db, MembershipService membership)
{
    /// <summary>Filters delivery state and projects rows before the web layer applies pagination.</summary>
    public IQueryable<MemberMessageSummaryDto> Messages(string? emailOrId, string? state)
    {
        var query = db.MemberMessages.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(emailOrId))
        {
            var normalized = new Microsoft.AspNetCore.Identity.UpperInvariantLookupNormalizer().NormalizeEmail(emailOrId.Trim());
            query = query.Where(x => x.UserId == emailOrId || db.Users.Any(u => u.Id == x.UserId && u.NormalizedEmail == normalized));
        }
        query = state switch
        {
            "failed" => query.Where(x => x.SentAt == null && x.CancelledAt == null && x.LastError != ""),
            "pending" => query.Where(x => x.SentAt == null && x.CancelledAt == null),
            "sent" => query.Where(x => x.SentAt != null),
            "cancelled" => query.Where(x => x.CancelledAt != null),
            _ => query,
        };
        return query.OrderByDescending(x => x.Id).Select(x => new MemberMessageSummaryDto(x.Id, x.UserId, x.Text, x.SentAt, x.CancelledAt, x.LastError, x.Attempts,
            db.MemberProfiles.Where(m => m.UserId == x.UserId).Select(m => m.Name).FirstOrDefault() ?? ""));
    }

    /// <summary>Lists unprocessed bot updates without loading their JSON bodies.</summary>
    public IQueryable<TelegramInboxSummaryDto> PendingInbox() => db.TelegramInbox.AsNoTracking()
        .Where(x => x.ProcessedAt == null).OrderByDescending(x => x.Id)
        .Select(x => new TelegramInboxSummaryDto(x.Id, x.ReceivedAt, x.Attempts));

    /// <summary>Moves an unsent, uncancelled delivery's availability to now; returns its owner for navigation.</summary>
    public async Task<string?> RetryAsync(long id, string actor, CancellationToken ct)
    {
        var message = await db.MemberMessages.SingleOrDefaultAsync(x => x.Id == id && x.SentAt == null && x.CancelledAt == null, ct);
        if (message is null)
            return null;
        message.AvailableAt = DateTimeOffset.UtcNow;
        membership.Audit(actor, "Notification.Retry", $"Member {message.UserId}; message {id}");
        await db.SaveChangesAsync(ct);
        return message.UserId;
    }

    /// <summary>Resets attempts and lease only for an unprocessed update, retaining the original explicit retry audit.</summary>
    public async Task RetryInboxAsync(long id, string actor, CancellationToken ct)
    {
        await db.TelegramInbox.Where(x => x.Id == id && x.ProcessedAt == null).ExecuteUpdateAsync(
            x => x.SetProperty(y => y.Attempts, 0).SetProperty(y => y.LockedUntil, (DateTimeOffset?)null), ct);
        membership.Audit(actor, "Telegram.Retry", "Update " + id);
        await db.SaveChangesAsync(ct);
    }
}
