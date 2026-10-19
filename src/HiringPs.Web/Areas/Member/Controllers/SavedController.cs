using System.Globalization;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Member.Controllers;

public class SavedController(HiringDb db, MembershipService membership) : MemberControllerBase(db)
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var model = await Db
            .SavedOpportunities.AsNoTracking()
            .Include(x => x.Opportunity)
            .Where(x => x.UserId == MemberId)
            .OrderByDescending(x => x.Id)
            .PageAsync(page, ct);
        ViewBag.AllowedDetails = await membership.DetailKindsAsync(MemberId, false, ct);
        Response.Headers.CacheControl = "no-store";
        return Request.Headers["X-Member-Async"] == "true"
            ? PartialView("_SavedResults", model)
            : View("Index", model);
    }

    [HttpPost]
    public async Task<IActionResult> Add(long id, string? returnUrl, CancellationToken ct)
    {
        if (
            !await Db.Opportunities.AnyAsync(
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
        if (
            !await Db.SavedOpportunities.AnyAsync(
                x => x.UserId == MemberId && x.OpportunityId == id,
                ct
            )
        )
        {
            var saved = new SavedOpportunity { UserId = MemberId, OpportunityId = id };
            Db.SavedOpportunities.Add(saved);
            try
            {
                await Db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // A second tab may have saved the same opportunity between the read and insert.
                Db.Entry(saved).State = EntityState.Detached;
                if (
                    !await Db.SavedOpportunities.AnyAsync(
                        x => x.UserId == MemberId && x.OpportunityId == id,
                        ct
                    )
                )
                    throw;
            }
        }
        Response.Headers.CacheControl = "no-store";
        if (Request.Headers["X-Member-Async"] == "true")
            return Json(
                new { saved = true, opportunityId = id.ToString(CultureInfo.InvariantCulture) }
            );
        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl!)
            : RedirectToAction(nameof(Index));
    }

    // Card actions use the opportunity ID; the saved-list Remove action uses a saved-row ID.
    [HttpPost]
    public async Task<IActionResult> Unsave(long id, string? returnUrl, CancellationToken ct)
    {
        await Db
            .SavedOpportunities.Where(x => x.UserId == MemberId && x.OpportunityId == id)
            .ExecuteDeleteAsync(ct);
        Response.Headers.CacheControl = "no-store";
        if (Request.Headers["X-Member-Async"] == "true")
            return Json(
                new { saved = false, opportunityId = id.ToString(CultureInfo.InvariantCulture) }
            );
        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl!)
            : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Remove(long id, int page = 1, CancellationToken ct = default)
    {
        await Db
            .SavedOpportunities.Where(x => x.UserId == MemberId && x.Id == id)
            .ExecuteDeleteAsync(ct);
        return Request.Headers["X-Member-Async"] == "true"
            ? await Index(page, ct)
            : RedirectToAction(nameof(Index), new { page });
    }
}
