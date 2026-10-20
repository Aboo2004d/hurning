using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>Reads the contact inbox and changes read/handled state without HTTP dependencies.</summary>
public sealed class ContactInboxService(HiringDb db)
{
    /// <summary>Normalizes the existing inbox filter; unknown values select all messages.</summary>
    public static string NormalizeStatus(string status) => status is "all" or "unread" or "read" or "handled" ? status : "all";

    /// <summary>Projects inbox summaries before pagination, retaining the original creation/id ordering.</summary>
    public IQueryable<ContactMessageSummaryDto> Messages(string status)
    {
        var query = db.ContactMessages.AsNoTracking();
        query = NormalizeStatus(status) switch
        {
            "unread" => query.Where(x => !x.IsRead && !x.IsHandled),
            "read" => query.Where(x => x.IsRead && !x.IsHandled),
            "handled" => query.Where(x => x.IsHandled),
            _ => query,
        };
        return query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new ContactMessageSummaryDto(x.Id, x.Name, x.Email, x.CreatedAt, x.IsRead, x.IsHandled));
    }

    /// <summary>Counts unread and unhandled messages independently of the selected list filter.</summary>
    public Task<int> UnreadCountAsync(CancellationToken ct) => db.ContactMessages.CountAsync(x => !x.IsRead && !x.IsHandled, ct);

    /// <summary>Marks an opened message as read, preserving the original save and concurrency behavior.</summary>
    public async Task<ContactMessageDetailsDto?> OpenAsync(long id, CancellationToken ct)
    {
        var message = await db.ContactMessages.FindAsync([id], ct);
        if (message is null)
            return null;
        if (!message.IsRead)
        {
            message.IsRead = true;
            await db.SaveChangesAsync(ct);
        }
        return new(message.Id, message.Name, message.Email, message.Message, message.CreatedAt, message.IsRead, message.IsHandled);
    }

    /// <summary>Changes handled state, marks the message read, and records the existing audit action.</summary>
    public async Task<RecordUpdateOutcome> SetHandledAsync(long id, bool handled, string actor, CancellationToken ct)
    {
        var message = await db.ContactMessages.FindAsync([id], ct);
        if (message is null)
            return RecordUpdateOutcome.NotFound;
        message.IsHandled = handled;
        message.IsRead = true;
        db.Audit.Add(new()
        {
            Actor = actor,
            Action = "Contact.Status",
            Detail = $"Message {id}; handled {handled}"
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return RecordUpdateOutcome.Conflict;
        }
        return RecordUpdateOutcome.Saved;
    }
}
