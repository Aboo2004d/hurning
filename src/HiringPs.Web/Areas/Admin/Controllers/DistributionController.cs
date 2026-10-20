using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

public sealed class DistributionTarget
{
    public string Target { get; init; } = "";
    public string Channel { get; init; } = "";
    public TelegramChannelSetting? Setting { get; init; }
}

public sealed class DistributionModel
{
    public bool BotReady { get; init; }
    public List<DistributionTarget> Targets { get; init; } = [];
    public AdminPage<SocialPublication> Posts { get; init; } = new();
    public bool IndexNowEnabled { get; init; }
    public bool IndexNowKeyConfigured { get; init; }
    public string IndexNowKeyUrl { get; init; } = "";
    public int IndexNowPending { get; init; }
    public int IndexNowFailing { get; init; }
    public DateTimeOffset? IndexNowLastSubmission { get; init; }
    public List<IndexNowEntry> IndexNowRecent { get; init; } = [];
}

// Telegram channel auto-publishing and IndexNow status. Sending happens in the Worker,
// except the explicit test post, which is a single admin-requested message.
[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DistributionController(
    HiringDb db,
    TelegramChannelPublisher telegram,
    IConfiguration configuration
) : Controller
{
    public async Task<IActionResult> Index(string? state, int page = 1, CancellationToken ct = default)
    {
        var settings = await db.TelegramChannelSettings.AsNoTracking().ToListAsync(ct);
        var posts = db.SocialPublications.AsNoTracking().Include(x => x.Opportunity)
            .Where(x => x.Provider == TelegramChannelTargets.Provider);
        if (state is "Pending" or "Published" or "Failed" or "Unknown" or "Skipped")
            posts = posts.Where(x => x.Status == state);
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
            Posts = await posts.OrderByDescending(x => x.CreatedAt).PageAsync(page, ct, "page", "#telegram-posts"),
            IndexNowEnabled = IndexNowSettings.Enabled(configuration),
            IndexNowKeyConfigured = key is not null,
            IndexNowKeyUrl = keyUrl,
            IndexNowPending = await db.IndexNowEntries.CountAsync(x => x.ProcessedAt == null || x.ProcessedAt < x.QueuedAt, ct),
            IndexNowFailing = await db.IndexNowEntries.CountAsync(x => x.Attempts > 0, ct),
            IndexNowLastSubmission = await db.IndexNowEntries.MaxAsync(x => x.SubmittedAt, ct),
            IndexNowRecent = await db.IndexNowEntries.AsNoTracking()
                .OrderByDescending(x => x.SubmittedAt ?? x.QueuedAt).Take(10).ToListAsync(ct),
        });
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(string target, bool enabled, CancellationToken ct)
    {
        if (!TelegramChannelTargets.IsTarget(target)) return BadRequest();
        var setting = await db.TelegramChannelSettings.FindAsync([target], ct);
        if (setting is null) { setting = new TelegramChannelSetting { Target = target }; db.TelegramChannelSettings.Add(setting); }
        if (setting.Enabled != enabled)
        {
            setting.Enabled = enabled;
            // Only opportunities first published after enabling are posted automatically.
            setting.EnabledAt = enabled ? DateTimeOffset.UtcNow : setting.EnabledAt;
            setting.Actor = User.Identity?.Name ?? "Admin";
            db.Audit.Add(new AuditEntry { Actor = setting.Actor, Action = "TelegramChannel.Enabled", Detail = target + ":" + enabled });
            await db.SaveChangesAsync(ct);
        }
        TempData["Notice"] = "SavedChanges";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Test(string target, CancellationToken ct)
    {
        if (!TelegramChannelTargets.IsTarget(target)) return BadRequest();
        TelegramChannelResult result;
        try
        {
            // A disconnected browser must not cancel recording the outcome.
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            result = await telegram.TestAsync(target, budget.Token);
        }
        catch (InvalidOperationException ex) when (ex.Message == "PublicOriginRequired")
        {
            result = new(TelegramChannelOutcome.Rejected, ex.Message);
        }
        db.Audit.Add(new AuditEntry { Actor = User.Identity?.Name ?? "Admin", Action = "TelegramChannel.Test",
            Detail = target + ":" + (result.Outcome == TelegramChannelOutcome.Sent ? "Sent" : result.Code) });
        await db.SaveChangesAsync(CancellationToken.None);
        TempData["Notice"] = result.Outcome == TelegramChannelOutcome.Sent ? "TelegramChannelTestSent" : result.Code;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Retry(Guid id, bool confirmUnknown, CancellationToken ct)
    {
        var post = await db.SocialPublications.SingleOrDefaultAsync(x => x.Id == id && x.Provider == TelegramChannelTargets.Provider, ct);
        if (post is null) return NotFound();
        if (post.Status is "Published" or "Pending") return RedirectToAction(nameof(Index));
        // An unknown result may already be in the channel: retry only after explicit confirmation.
        if (post.Status == "Unknown" && !confirmUnknown) { TempData["Notice"] = "TelegramRetryConfirm"; return RedirectToAction(nameof(Index)); }
        post.Status = "Pending";
        post.Attempts = 0;
        post.NextAttemptAt = DateTimeOffset.UtcNow;
        post.Error = "";
        db.Audit.Add(new AuditEntry { Actor = User.Identity?.Name ?? "Admin", Action = "TelegramChannel.Retry", Detail = id.ToString() });
        await db.SaveChangesAsync(ct);
        TempData["Notice"] = "SavedChanges";
        return RedirectToAction(nameof(Index));
    }
}
