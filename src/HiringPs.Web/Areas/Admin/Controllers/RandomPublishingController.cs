using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin"), Route("Admin/RandomPublishing"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RandomPublishingController(HiringDb db, RandomPublicationSettingsService settings) : Controller
{
    /// <summary>يعرض إعداد كل وجهة وسجل مواعيد النشر المرقّم.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var history = await db.RandomPublicationRuns.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new RandomPublicationRunDto(x.Id, x.Provider, x.ScheduledAt, x.OpportunityId,
                db.Opportunities.Where(o => o.Id == x.OpportunityId).Select(o => o.Title).FirstOrDefault() ?? "",
                x.State, x.ResultCode)).PageAsync(page, ct);
        return View(new RandomPublishingPage(await settings.ReadAsync(ct), history));
    }
    /// <summary>يحفظ جدول وجهة واحدة، ولا ينشر شيئًا أثناء طلب HTTP.</summary>
    [HttpPost("Save"), RequestSizeLimit(16384)]
    public async Task<IActionResult> Save(RandomPublishingInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { TempData["Notice"] = "RandomSettingsInvalid"; return RedirectToAction(nameof(Index)); }
        var error = await settings.SaveAsync(new(input.Provider, input.Enabled, input.DailyCount,
            input.MaxRemainingDays, input.Times, input.Version ?? "", false, ""), User.Identity?.Name ?? "Admin", ct);
        TempData["Notice"] = error ?? "RandomSettingsSaved";
        return RedirectToAction(nameof(Index));
    }
}
