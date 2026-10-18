using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Member.Controllers;

public class SavedController(HiringDb db) : MemberControllerBase(db)
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) =>
        View(
            await Db
                .SavedOpportunities.AsNoTracking()
                .Include(x => x.Opportunity)
                .Where(x => x.UserId == MemberId)
                .OrderByDescending(x => x.Id)
                .PageAsync(page, ct)
        );

    [HttpPost]
    public async Task<IActionResult> Add(long id, CancellationToken ct)
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
            Db.SavedOpportunities.Add(
                new SavedOpportunity { UserId = MemberId, OpportunityId = id }
            );
            await Db.SaveChangesAsync(ct);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Remove(long id, CancellationToken ct)
    {
        await Db
            .SavedOpportunities.Where(x => x.UserId == MemberId && x.Id == id)
            .ExecuteDeleteAsync(ct);
        return RedirectToAction(nameof(Index));
    }
}
