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

public class AdminController : AdminControllerBase
{
    public AdminController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    public async Task<IActionResult> Index(
        int runsPage = 1,
        int itemsPage = 1,
        CancellationToken ct = default
    )
    {
        var runs = await db
            .Runs.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .PageAsync(runsPage, ct, "runsPage", "#runs");
        var items = await db
            .SourceItems.AsNoTracking()
            .Include(x => x.Source)
            .Where(x => x.Status != ProcessingStatus.Published)
            .OrderByDescending(x => x.Id)
            .PageAsync(itemsPage, ct, "itemsPage", "#queue");
        return View(
            new HiringPs.Web.Controllers.AdminDashboard
            {
                Published = await db.Opportunities.CountAsync(
                    x => x.Status == PublicationStatus.Published,
                    ct
                ),
                Review = await db.Opportunities.CountAsync(
                    x => x.Status == PublicationStatus.NeedsReview,
                    ct
                ),
                Queued = await db.SourceItems.CountAsync(
                    x =>
                        x.Status == ProcessingStatus.Discovered
                        || x.Status == ProcessingStatus.AwaitingAi
                        || x.Status == ProcessingStatus.Retry,
                    ct
                ),
                Reports = await db.Reports.CountAsync(x => !x.Resolved, ct),
                Heartbeat = await db
                    .Heartbeats.AsNoTracking()
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync(ct),
                Runs = runs.Items,
                RunsPager = runs.Pager,
                Items = items.Items,
                ItemsPager = items.Pager,
            }
        );
    }
}
