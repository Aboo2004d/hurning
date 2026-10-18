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

public class AiUsageController : AdminControllerBase
{
    public AiUsageController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    public async Task<IActionResult> AiUsage(
        int days = 30,
        int page = 1,
        int groupsPage = 1,
        CancellationToken ct = default
    )
    {
        days = Math.Clamp(days, 1, 365);
        var since = DateTimeOffset.UtcNow.AddDays(-days);
        var query = db.AiUsage.AsNoTracking().Where(x => x.StartedAt >= since);
        var items = await query
            .OrderByDescending(x => x.Id)
            .PageAsync(page, ct, "page", "#ai-requests");
        var groupsQuery = query
            .GroupBy(x => new { x.PromptVersionId, x.Model })
            .Select(g => new AiUsageTotals
            {
                PromptVersionId = g.Key.PromptVersionId,
                Model = g.Key.Model,
                Requests = g.Count(),
                Succeeded = g.Count(x => x.Success == true),
                Failed = g.Count(x => x.Success == false),
                Unfinished = g.Count(x => x.Success == null),
                UnknownTokenRequests = g.Count(x => x.TotalTokens == null),
                InputTokens =
                    g.Count(x => x.InputTokens != null) > 0
                        ? g.Sum(x => (long?)x.InputTokens)
                        : null,
                OutputTokens =
                    g.Count(x => x.OutputTokens != null) > 0
                        ? g.Sum(x => (long?)x.OutputTokens)
                        : null,
                TotalTokens =
                    g.Count(x => x.TotalTokens != null) > 0
                        ? g.Sum(x => (long?)x.TotalTokens)
                        : null,
            });
        var groups = await groupsQuery
            .OrderByDescending(x => x.PromptVersionId)
            .ThenBy(x => x.Model)
            .PageAsync(groupsPage, ct, "groupsPage", "#ai-totals");
        return View(
            new AiUsageDashboard
            {
                Days = days,
                Items = items.Items,
                ItemsPager = items.Pager,
                Groups = groups.Items,
                GroupsPager = groups.Pager,
            }
        );
    }
}
