using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class NotificationsController(HiringDb db, MembershipService membership) : Controller
{
    public async Task<IActionResult> Index(
        string? userId,
        string? state,
        int page = 1,
        int inboxPage = 1,
        CancellationToken ct = default
    )
    {
        var query = db.MemberMessages.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(x => x.UserId == userId);
        query = state switch
        {
            "failed" => query.Where(x =>
                x.SentAt == null && x.CancelledAt == null && x.LastError != ""
            ),
            "pending" => query.Where(x => x.SentAt == null && x.CancelledAt == null),
            "sent" => query.Where(x => x.SentAt != null),
            "cancelled" => query.Where(x => x.CancelledAt != null),
            _ => query,
        };
        ViewBag.Inbox = await db
            .TelegramInbox.AsNoTracking()
            .Where(x => x.ProcessedAt == null)
            .Select(x => new TelegramInbox
            {
                Id = x.Id,
                ReceivedAt = x.ReceivedAt,
                Attempts = x.Attempts,
            })
            .OrderByDescending(x => x.Id)
            .PageAsync(inboxPage, ct, "inboxPage", "#bot-inbox");
        return View(await query.OrderByDescending(x => x.Id).PageAsync(page, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Retry(long id, CancellationToken ct)
    {
        var message = await db.MemberMessages.SingleOrDefaultAsync(
            x => x.Id == id && x.SentAt == null && x.CancelledAt == null,
            ct
        );
        if (message is null)
            return NotFound();
        message.AvailableAt = DateTimeOffset.UtcNow;
        membership.Audit(
            User.Identity?.Name ?? "Admin",
            "Notification.Retry",
            $"Member {message.UserId}; message {id}"
        );
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Index), new { userId = message.UserId });
    }

    [HttpPost]
    public async Task<IActionResult> RetryInbox(long id, CancellationToken ct)
    {
        await db
            .TelegramInbox.Where(x => x.Id == id && x.ProcessedAt == null)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.Attempts, 0)
                        .SetProperty(y => y.LockedUntil, (DateTimeOffset?)null),
                ct
            );
        membership.Audit(User.Identity?.Name ?? "Admin", "Telegram.Retry", "Update " + id);
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Index));
    }
}
