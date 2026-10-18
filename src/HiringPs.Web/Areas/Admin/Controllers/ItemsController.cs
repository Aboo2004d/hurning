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

public class ItemsController : AdminControllerBase
{
    public ItemsController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    public async Task<IActionResult> Item(long id, CancellationToken ct)
    {
        var item = await db
            .SourceItems.AsNoTracking()
            .Include(x => x.Source)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost]
    public async Task<IActionResult> Retry(long id, CancellationToken ct)
    {
        var item = await db
            .SourceItems.Include(x => x.Source)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null)
            return NotFound();
        // PDF retries fetch the original again instead of reusing a damaged historical transcription.
        var refreshPdf = PdfRetry.NeedsFreshContent(item);
        item.Status =
            !refreshPdf && item.Content.Length > 0
                ? ProcessingStatus.AwaitingAi
                : ProcessingStatus.Discovered;
        if (refreshPdf)
            item.ExtractionJson = "";
        // Reset retry state
        item.RetryAt = null;
        item.Attempts = 0;
        // Clear previous processing error
        item.Error = string.Empty;
        Audit("Item.Retry", $"Item {id}");
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Item), new { id });
    }
}
