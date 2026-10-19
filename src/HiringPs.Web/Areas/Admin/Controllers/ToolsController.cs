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

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ToolsController : AdminControllerBase
{
    public ToolsController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    [HttpGet]
    public async Task<IActionResult> Tools(int page = 1, CancellationToken ct = default)
    {
        var requests = await db
            .ToolCheckRequests.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .PageAsync(page, ct);
        var now = DateTimeOffset.UtcNow;
        var model = new ToolDashboard
        {
            Now = now,
            HasPendingChecks = await db.ToolCheckRequests.AnyAsync(
                x => x.Status == ToolRequestStatus.Pending || x.Status == ToolRequestStatus.Running, ct),
            GoogleInteractive = await db.ToolCheckResults.AsNoTracking()
                .Where(x => x.Tool == "GoogleInteractive" && x.State == ToolState.Ready)
                .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct),
            Worker = await db
                .ToolWorkers.AsNoTracking()
                .OrderByDescending(x => x.At)
                .FirstOrDefaultAsync(ct),
            FreshWorkers = await db.ToolWorkers.CountAsync(x => x.At >= now.AddSeconds(-60), ct),
            Requests = requests.Items,
            RequestsPager = requests.Pager,
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
        QueueToolCheckAsync(ToolCheckKind.All, ct);

    [HttpGet]
    public Task<IActionResult> CheckTool(string tool, CancellationToken ct)
    {
        if (tool != "Collector" && tool != "AiLive" && !ToolMonitoringRules.StandardTools.Contains(tool))
            return Task.FromResult<IActionResult>(BadRequest());
        return Enum.TryParse<ToolCheckKind>(tool, out var kind)
            ? QueueToolCheckAsync(kind, ct) : Task.FromResult<IActionResult>(BadRequest());
    }

    [HttpGet]
    public Task<IActionResult> CheckAi(CancellationToken ct) =>
        QueueToolCheckAsync(ToolCheckKind.AiLive, ct);

    [HttpGet]
    public Task<IActionResult> CheckIntegration(string provider, CancellationToken ct) => provider switch
    {
        "GoogleLogin" => QueueToolCheckAsync(ToolCheckKind.GoogleLogin, ct),
        "Facebook" => QueueToolCheckAsync(ToolCheckKind.Facebook, ct),
        "LinkedIn" => QueueToolCheckAsync(ToolCheckKind.LinkedIn, ct),
        _ => Task.FromResult<IActionResult>(BadRequest())
    };

    private async Task<IActionResult> QueueToolCheckAsync(ToolCheckKind kind, CancellationToken ct)
    {
        var queued = await ToolCheckQueue.EnqueueAsync(
            db,
            configuration,
            kind,
            User.Identity?.Name ?? "Admin",
            ct
        );
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { queued });
        TempData["Notice"] = queued ? "ToolsCheckQueued" : "ToolsCheckAlreadyQueued";
        // A check changes state; always display its result with a separate GET.
        // 303 explicitly prevents refresh from submitting the check again.
        var destination = Url.Action(nameof(Tools), "Tools", new { area = "Admin" })
            ?? throw new InvalidOperationException("Unable to generate the tools dashboard URL.");
        Response.Headers.Location = destination;
        return StatusCode(StatusCodes.Status303SeeOther);
    }
}

