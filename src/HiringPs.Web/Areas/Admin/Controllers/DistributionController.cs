using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

// Telegram channel auto-publishing and IndexNow status. Sending happens in the Worker,
// except the explicit test post, which is a single admin-requested message.
[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DistributionController(
    HiringDb db,
    TelegramChannelPublisher telegram,
    IConfiguration configuration,
    DistributionReadService? readService = null,
    TelegramChannelManagementService? managementService = null
) : Controller
{
    private DistributionReadService Reader => readService ?? new(db);
    private TelegramChannelManagementService Commands => managementService ?? new(db, telegram);

    public async Task<IActionResult> Index(string? state, int page = 1, CancellationToken ct = default)
    {
        var settings = await Reader.SettingsAsync(ct);
        var posts = Reader.Posts(state);
        var indexNow = await Reader.IndexNowAsync(ct);
        var key = IndexNowSettings.Key(configuration);
        string keyUrl = "";
        try { if (key is not null) keyUrl = PublicUrls.Origin(configuration) + "/" + key + ".txt"; }
        catch (InvalidOperationException) { }
        return View(new DistributionModel
        {
            BotReady = telegram.Ready,
            Targets = TelegramChannelTargets.All.Select(target => new DistributionTarget
            {
                Target = target,
                Channel = TelegramChannelTargets.Channel(configuration, target),
                Setting = settings.SingleOrDefault(x => x.Target == target),
            }).ToList(),
            Posts = await posts.PageAsync(page, ct, "page", "#telegram-posts"),
            IndexNowEnabled = IndexNowSettings.Enabled(configuration),
            IndexNowKeyConfigured = key is not null,
            IndexNowKeyUrl = keyUrl,
            IndexNowPending = indexNow.Pending,
            IndexNowFailing = indexNow.Failing,
            IndexNowLastSubmission = indexNow.LastSubmission,
            IndexNowRecent = indexNow.Recent,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(string target, bool enabled, CancellationToken ct)
    {
        var result = await Commands.ToggleAsync(target, enabled, User.Identity?.Name ?? "Admin", ct);
        if (result.Outcome == TelegramChannelActionOutcome.InvalidRequest) return BadRequest();
        if (result.Outcome == TelegramChannelActionOutcome.NotFound) return NotFound();
        if (result.Notice is not null) TempData["Notice"] = result.Notice;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Test(string target, CancellationToken ct)
    {
        var result = await Commands.TestAsync(target, User.Identity?.Name ?? "Admin", ct);
        if (result.Outcome == TelegramChannelActionOutcome.InvalidRequest) return BadRequest();
        if (result.Outcome == TelegramChannelActionOutcome.NotFound) return NotFound();
        if (result.Notice is not null) TempData["Notice"] = result.Notice;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Retry(Guid id, bool confirmUnknown, CancellationToken ct)
    {
        var result = await Commands.RetryAsync(id, confirmUnknown, User.Identity?.Name ?? "Admin", ct);
        if (result.Outcome == TelegramChannelActionOutcome.InvalidRequest) return BadRequest();
        if (result.Outcome == TelegramChannelActionOutcome.NotFound) return NotFound();
        if (result.Notice is not null) TempData["Notice"] = result.Notice;
        return RedirectToAction(nameof(Index));
    }

}
