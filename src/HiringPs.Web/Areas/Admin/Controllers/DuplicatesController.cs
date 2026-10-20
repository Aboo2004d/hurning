using System.Globalization;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Areas.Admin.Controllers;

// Near-match candidates from extraction: one-click merge (undoable in merge history) or dismiss.
public class DuplicatesController : AdminControllerBase
{
    public DuplicatesController(HiringDb database, UrlPolicy urls, IConfiguration settings)
        : base(database, urls, settings) { }

    private static bool Arabic => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

    private DuplicateManagementService Duplicates => new(db);

    public async Task<IActionResult> Index(DuplicateCandidateStatus status = DuplicateCandidateStatus.Open,
        int page = 1, CancellationToken ct = default)
    {
        ViewBag.Status = status;
        return View(await Duplicates.Candidates(status).PageAsync(page, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Merge(long id, bool reverse, CancellationToken ct)
    {
        var result = await Duplicates.MergeAsync(id, reverse, User.Identity?.Name ?? "Admin", ct);
        if (result.Outcome == DuplicateActionOutcome.NotFound) return NotFound();
        if (result.Outcome == DuplicateActionOutcome.Unchanged) return RedirectToAction(nameof(Index));
        var duplicate = result.DuplicateId;
        var target = result.TargetId;
        TempData["PipelineNotice"] = result.Outcome == DuplicateActionOutcome.Merged
            ? (Arabic ? $"تم دمج {duplicate} في {target}. يمكن التراجع من سجل الدمج." : $"Merged {duplicate} into {target}. Undo is available in merge history.")
            : (Arabic ? "تعذر الدمج: النوع مختلف أو السجل مدمج مسبقًا أو تغيّر أثناء العملية." : "Merge failed: different kind, already merged, or changed meanwhile.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Dismiss(long id, CancellationToken ct)
    {
        if (!await Duplicates.DismissAsync(id, User.Identity?.Name ?? "Admin", ct)) return NotFound();
        return RedirectToAction(nameof(Index));
    }
}
