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

public class MergesController : AdminControllerBase
{
    public MergesController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    public async Task<IActionResult> Merges(
        long? opportunityId,
        int page = 1,
        CancellationToken ct = default
    )
    {
        var query = db.MergeOperations.AsNoTracking();
        if (opportunityId is { } id)
            query = query.Where(x => x.DuplicateId == id || x.TargetId == id);
        return View(await query.OrderByDescending(x => x.Id).PageAsync(page, ct));
    }

    [HttpPost]
    public async Task<IActionResult> UndoMerge(long id, CancellationToken ct)
    {
        try
        {
            TempData["Notice"] = await MergeHistory.UndoAsync(
                db,
                id,
                User.Identity?.Name ?? "Admin",
                ct
            )
                ? "MergeUndone"
                : "MergeUndoConflict";
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Notice"] = "MergeUndoConflict";
        }
        return RedirectToAction(nameof(Merges));
    }
}
