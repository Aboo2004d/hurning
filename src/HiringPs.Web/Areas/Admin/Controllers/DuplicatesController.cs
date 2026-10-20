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

    public async Task<IActionResult> Index(
        DuplicateCandidateStatus status = DuplicateCandidateStatus.Open,
        int page = 1,
        CancellationToken ct = default
    )
    {
        ViewBag.Status = status;
        var query = db
            .DuplicateCandidates.AsNoTracking()
            .Include(x => x.Opportunity)
            .Include(x => x.Candidate)
            .Where(x => x.Status == status);
        // Open pairs where one side was already merged/hidden elsewhere are no longer actionable.
        if (status == DuplicateCandidateStatus.Open)
            query = query.Where(x =>
                x.Opportunity.Status != PublicationStatus.Hidden
                && x.Candidate.Status != PublicationStatus.Hidden
            );
        return View(await query.OrderByDescending(x => x.Id).PageAsync(page, ct));
    }

    // The newer record (Opportunity) is merged into the earlier one (Candidate) unless reversed.
    [HttpPost]
    public async Task<IActionResult> Merge(long id, bool reverse, CancellationToken ct)
    {
        var pair = await db.DuplicateCandidates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (pair is null)
            return NotFound();
        if (pair.Status != DuplicateCandidateStatus.Open)
            return RedirectToAction(nameof(Index));
        var duplicate = reverse ? pair.CandidateId : pair.OpportunityId;
        var target = reverse ? pair.OpportunityId : pair.CandidateId;
        bool merged;
        try
        {
            merged = await MergeHistory.MergeAsync(
                db,
                duplicate,
                target,
                User.Identity?.Name ?? "Admin",
                ct
            );
        }
        catch (DbUpdateConcurrencyException)
        {
            merged = false;
        }
        TempData["PipelineNotice"] = merged
            ? (Arabic ? $"تم دمج {duplicate} في {target}. يمكن التراجع من سجل الدمج." : $"Merged {duplicate} into {target}. Undo is available in merge history.")
            : (Arabic ? "تعذر الدمج: النوع مختلف أو السجل مدمج مسبقًا أو تغيّر أثناء العملية." : "Merge failed: different kind, already merged, or changed meanwhile.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Dismiss(long id, CancellationToken ct)
    {
        var pair = await db.DuplicateCandidates.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (pair is null)
            return NotFound();
        if (pair.Status == DuplicateCandidateStatus.Open)
        {
            pair.Status = DuplicateCandidateStatus.Dismissed;
            pair.ResolvedAt = DateTimeOffset.UtcNow;
            pair.ResolvedBy = (User.Identity?.Name ?? "Admin").Truncate(256);
            Audit("Duplicate.Dismiss", $"{pair.OpportunityId} / {pair.CandidateId}");
            await db.SaveChangesAsync(ct);
        }
        return RedirectToAction(nameof(Index));
    }
}
