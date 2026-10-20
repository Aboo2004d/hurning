using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class AdminController : AdminControllerBase
{
    public AdminController(
        HiringDb database,
        UrlPolicy urls,
        IConfiguration settings,
        AdminDashboardReadService dashboard,
        SourceItemAdministrationService items
    )
        : base(database, urls, settings)
    {
        this.dashboard = dashboard;
        this.items = items;
    }

    private readonly AdminDashboardReadService dashboard;
    private readonly SourceItemAdministrationService items;

    public async Task<IActionResult> Index(int runsPage = 1, int itemsPage = 1, CancellationToken ct = default)
    {
        var runs = await dashboard.Runs().PageAsync(runsPage, ct, "runsPage", "#runs");
        var queue = await items.Queue().PageAsync(itemsPage, ct, "itemsPage", "#queue");
        var counts = await dashboard.CountsAsync(ct);
        return View(new HiringPs.Web.Controllers.AdminDashboard
        {
            Published = counts.Published,
            Review = counts.Review,
            Queued = counts.Queued,
            Reports = counts.Reports,
            Heartbeat = await dashboard.HeartbeatAsync(ct),
            Runs = runs.Items,
            RunsPager = runs.Pager,
            Items = queue.Items,
            ItemsPager = queue.Pager,
        });
    }
}
