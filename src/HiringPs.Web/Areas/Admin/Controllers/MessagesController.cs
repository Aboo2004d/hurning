using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class MessagesController : AdminControllerBase
{
    public MessagesController(
        HiringDb database,
        UrlPolicy urls,
        IConfiguration settings,
        ContactInboxService service
    )
        : base(database, urls, settings) => Inbox = service;

    private ContactInboxService Inbox
    {
        get;
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Messages(string status = "all", int page = 1, CancellationToken ct = default)
    {
        status = ContactInboxService.NormalizeStatus(status);
        var messages = await Inbox.Messages(status).PageAsync(page, ct);
        return View(new ContactInbox
        {
            Items = messages.Items,
            Status = status,
            Count = messages.Pager.TotalRecords,
            Pager = messages.Pager,
            UnreadCount = await Inbox.UnreadCountAsync(ct),
        });
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Message(long id, CancellationToken ct)
    {
        var message = await Inbox.OpenAsync(id, ct);
        return message is null ? NotFound() : View(message);
    }

    [HttpPost]
    public async Task<IActionResult> MessageStatus(long id, bool handled, CancellationToken ct)
    {
        var result = await Inbox.SetHandledAsync(id, handled, User.Identity?.Name ?? "Admin", ct);
        if (result == RecordUpdateOutcome.NotFound)
            return NotFound();
        if (result == RecordUpdateOutcome.Conflict)
            TempData["Notice"] = "ContactChanged";
        return RedirectToAction(nameof(Message), new
        {
            id
        });
    }
}
