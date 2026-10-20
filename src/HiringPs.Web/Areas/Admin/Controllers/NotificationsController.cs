using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class NotificationsController(NotificationAdministrationService notifications) : Controller
{
    private NotificationAdministrationService Notifications => notifications;

    public async Task<IActionResult> Index(string? userId, string? state, int page = 1, int inboxPage = 1, CancellationToken ct = default)
    {
        ViewBag.Inbox = await Notifications.PendingInbox().PageAsync(inboxPage, ct, "inboxPage", "#bot-inbox");
        return View(await Notifications.Messages(userId, state).PageAsync(page, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Retry(long id, CancellationToken ct)
    {
        var userId = await Notifications.RetryAsync(id, User.Identity?.Name ?? "Admin", ct);
        return userId is null ? NotFound() : RedirectToAction(nameof(Index), new
        {
            userId
        });
    }

    [HttpPost]
    public async Task<IActionResult> RetryInbox(long id, CancellationToken ct)
    {
        await Notifications.RetryInboxAsync(id, User.Identity?.Name ?? "Admin", ct);
        return RedirectToAction(nameof(Index));
    }
}
