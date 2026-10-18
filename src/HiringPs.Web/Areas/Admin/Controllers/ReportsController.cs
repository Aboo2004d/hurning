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

public class ReportsController : AdminControllerBase
{
    public ReportsController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    public async Task<IActionResult> Reports(int page = 1, CancellationToken ct = default) =>
        View(
            await db
                .Reports.AsNoTracking()
                .Include(x => x.Opportunity)
                .OrderBy(x => x.Resolved)
                .ThenByDescending(x => x.Id)
                .PageAsync(page, ct)
        );

    [HttpPost]
    public async Task<IActionResult> Resolve(long id, CancellationToken ct)
    {
        var report = await db.Reports.FindAsync([id], ct);
        if (report is null)
            return NotFound();
        report.Resolved = true;
        Audit("Report.Resolve", $"Report {id}");
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Reports));
    }
}
