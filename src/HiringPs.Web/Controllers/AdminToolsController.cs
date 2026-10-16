using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

public partial class AdminController
{
    public async Task<IActionResult> Tools(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var model = new ToolDashboard
        {
            Now = now,
            Worker = await db
                .ToolWorkers.AsNoTracking()
                .OrderByDescending(x => x.At)
                .FirstOrDefaultAsync(ct),
            FreshWorkers = await db.ToolWorkers.CountAsync(x => x.At >= now.AddSeconds(-60), ct),
            Requests = await db
                .ToolCheckRequests.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Take(20)
                .ToListAsync(ct),
            LastAi = await db
                .AiUsage.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct),
            LastRun = await db
                .Runs.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct),
            Waiting = await db.SourceItems.CountAsync(
                x =>
                    x.Status == ProcessingStatus.Discovered
                    || x.Status == ProcessingStatus.AwaitingAi
                    || x.Status == ProcessingStatus.Retry,
                ct
            ),
            Review = await db.SourceItems.CountAsync(
                x => x.Status == ProcessingStatus.NeedsReview,
                ct
            ),
            EnabledSources = await db.Sources.CountAsync(x => x.Enabled && x.UsageApproved, ct),
        };
        foreach (var key in ToolMonitoringRules.StandardTools.Append("AiLive"))
        {
            var latest = await db
                .ToolCheckResults.AsNoTracking()
                .Where(x => x.Tool == key)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct);
            if (latest is not null)
                model.Results.Add(latest);
        }
        return View(model);
    }

    [HttpPost]
    public Task<IActionResult> CheckTools(CancellationToken ct) =>
        QueueToolCheckAsync(ToolCheckKind.Standard, ct);

    [HttpPost]
    public Task<IActionResult> CheckAi(CancellationToken ct) =>
        QueueToolCheckAsync(ToolCheckKind.AiLive, ct);

    private async Task<IActionResult> QueueToolCheckAsync(ToolCheckKind kind, CancellationToken ct)
    {
        var queued = await ToolCheckQueue.EnqueueAsync(
            db,
            configuration,
            kind,
            User.Identity?.Name ?? "Admin",
            ct
        );
        TempData["Notice"] = queued ? "ToolsCheckQueued" : "ToolsCheckAlreadyQueued";
        return RedirectToAction(nameof(Tools));
    }
}
