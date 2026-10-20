using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

public class AiUsageController : AdminControllerBase
{
    public AiUsageController(
        HiringDb database,
        UrlPolicy urls,
        IConfiguration settings,
        AiUsageReadService usage,
        AiPauseReadService pauses
    )
        : base(database, urls, settings)
    {
        this.usage = usage;
        this.pauses = pauses;
    }

    private readonly AiUsageReadService usage;
    private readonly AiPauseReadService pauses;

    public async Task<IActionResult> AiUsage(int days = 30, int page = 1, int groupsPage = 1, CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";
        days = Math.Clamp(days, 1, 365);
        var since = DateTimeOffset.UtcNow.AddDays(-days);
        var items = await usage.Requests(since).PageAsync(page, ct, "page", "#ai-requests");
        var groups = await usage.Totals(since).OrderByDescending(x => x.PromptVersionId).ThenBy(x => x.Model)
            .PageAsync(groupsPage, ct, "groupsPage", "#ai-totals");
        return View(new AiUsageDashboard
        {
            Days = days,
            PauseStatus = await pauses.ReadAsync(ct),
            Items = items.Items,
            ItemsPager = items.Pager,
            Groups = groups.Items,
            GroupsPager = groups.Pager,
        });
    }
}
