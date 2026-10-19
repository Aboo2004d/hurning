using HiringPs.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Controllers;

public class OpportunitiesController(HiringDb db, MembershipService membership) : Controller
{
    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var item = await db
            .Opportunities.AsNoTracking()
            .Include(x => x.SourceLinks)
                .ThenInclude(x => x.SourceItem)
                    .ThenInclude(x => x.Source)
            .FirstOrDefaultAsync(
                x =>
                    x.Id == id
                    && (
                        x.Status == PublicationStatus.Published
                        || x.Status == PublicationStatus.Archived
                    ),
                ct
            );
        if (item is null)
            return NotFound();
        var allowed = await membership.DetailKindsAsync(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            User.IsInRole("Admin"),
            ct
        );
        if (!allowed.Contains(item.Kind))
        {
            Response.StatusCode = 403;
            return View("Restricted", item.Kind);
        }
        if (User.Identity?.IsAuthenticated == true && !User.IsInRole("Admin"))
        {
            var memberId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            ViewBag.IsSaved = await db
                .SavedOpportunities.AsNoTracking()
                .AnyAsync(x => x.UserId == memberId && x.OpportunityId == id, ct);
            Response.Headers.CacheControl = "no-store";
        }
        ViewData["ListingKind"] = item.Kind;
        ViewBag.VerifiedDates = await db
            .OpportunityDates.AsNoTracking()
            .Where(x => x.OpportunityId == item.Id)
            .OrderByDescending(x => x.RecordedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
        return View(item);
    }

    [HttpPost, EnableRateLimiting("public")]
    public async Task<IActionResult> Report(
        long id,
        string reason,
        string? details,
        CancellationToken ct
    )
    {
        if (
            reason is not ("expired" or "duplicate" or "incorrect" or "broken" or "other")
            || (details?.Length ?? 0) > 1000
        )
            return BadRequest();
        if (
            !await db.Opportunities.AnyAsync(
                x =>
                    x.Id == id
                    && (
                        x.Status == PublicationStatus.Published
                        || x.Status == PublicationStatus.Archived
                    ),
                ct
            )
        )
            return NotFound();
        db.Reports.Add(
            new()
            {
                OpportunityId = id,
                Reason = reason,
                Details = details ?? "",
            }
        );
        await db.SaveChangesAsync(ct);
        TempData["Notice"] = "ReportReceived";
        return RedirectToAction(nameof(Details), new { id });
    }
}
