using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class MessagesController : AdminControllerBase
{
    public MessagesController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Messages(
        string status = "all",
        int page = 1,
        CancellationToken ct = default
    )
    {
        if (status is not ("all" or "unread" or "read" or "handled"))
            status = "all";
        var query = db.ContactMessages.AsNoTracking();
        query = status switch
        {
            "unread" => query.Where(x => !x.IsRead && !x.IsHandled),
            "read" => query.Where(x => x.IsRead && !x.IsHandled),
            "handled" => query.Where(x => x.IsHandled),
            _ => query,
        };
        var count = await query.CountAsync(ct);
        page = AdminPager.Normalize(page, count);
        return View(
            new HiringPs.Web.Models.ContactInbox
            {
                Items = await query
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                    .Skip((page - 1) * AdminPager.PageSize)
                    .Take(AdminPager.PageSize)
                    .ToListAsync(ct),
                Status = status,
                Count = count,
                Pager = new() { Page = page, TotalRecords = count },
                UnreadCount = await db.ContactMessages.CountAsync(
                    x => !x.IsRead && !x.IsHandled,
                    ct
                ),
            }
        );
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Message(long id, CancellationToken ct)
    {
        var message = await db.ContactMessages.FindAsync([id], ct);
        if (message is null)
            return NotFound();
        if (!message.IsRead)
        {
            message.IsRead = true;
            await db.SaveChangesAsync(ct);
        }

        return View(message);
    }

    [HttpPost]
    public async Task<IActionResult> MessageStatus(long id, bool handled, CancellationToken ct)
    {
        var message = await db.ContactMessages.FindAsync([id], ct);
        if (message is null)
            return NotFound();
        message.IsHandled = handled;
        message.IsRead = true;
        Audit("Contact.Status", $"Message {id}; handled {handled}");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Notice"] = "ContactChanged";
        }

        return RedirectToAction(nameof(Message), new { id });
    }
}
