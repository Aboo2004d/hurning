using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class ItemsController : AdminControllerBase
{
    public ItemsController(
        HiringDb database,
        UrlPolicy urls,
        IConfiguration settings,
        SourceItemAdministrationService service
    )
        : base(database, urls, settings) => Items = service;

    private SourceItemAdministrationService Items
    {
        get;
    }

    public async Task<IActionResult> Item(long id, CancellationToken ct)
    {
        var item = await Items.ReadAsync(id, ct);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost]
    public async Task<IActionResult> Retry(long id, CancellationToken ct)
    {
        if (!await Items.RetryAsync(id, User.Identity?.Name ?? "Admin", ct))
            return NotFound();
        return RedirectToAction(nameof(Item), new
        {
            id
        });
    }
}
