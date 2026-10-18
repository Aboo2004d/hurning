using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class SubscriptionsController(HiringDb db, MembershipService membership) : Controller
{
    public async Task<IActionResult> Index(
        string? q,
        string? status,
        int page = 1,
        CancellationToken ct = default
    )
    {
        var now = DateTimeOffset.UtcNow;
        var query = db.MemberSubscriptions.AsNoTracking().Include(x => x.PlanVersion).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x =>
                x.UserId == q
                || db.MemberProfiles.Any(m =>
                    m.UserId == x.UserId && (m.Name.Contains(q) || m.Mobile.Contains(q))
                )
            );
        query = status switch
        {
            "current" => query.Where(x => !x.Cancelled && x.StartsAt <= now && x.EndsAt > now),
            "scheduled" => query.Where(x => !x.Cancelled && x.StartsAt > now),
            "expired" => query.Where(x => !x.Cancelled && x.EndsAt <= now),
            "cancelled" => query.Where(x => x.Cancelled),
            _ => query,
        };
        ViewBag.Plans = await db
            .MembershipPlanVersions.AsNoTracking()
            .Where(x => db.MembershipPlans.Any(p => p.CurrentVersionId == x.Id))
            .ToListAsync(ct);
        return View(await query.OrderByDescending(x => x.Id).PageAsync(page, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Grant(
        string userId,
        int versionId,
        int? days,
        string reason,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(reason) || reason.Length > 1500)
            return BadRequest();
        await membership.GrantAsync(
            userId,
            versionId,
            days,
            User.Identity?.Name ?? "Admin",
            reason ?? "",
            ct
        );
        return RedirectToAction(nameof(Index), new { q = userId });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(long id, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1500)
            return BadRequest();
        var owner = await db
            .MemberSubscriptions.Where(x => x.Id == id)
            .Select(x => x.UserId)
            .SingleAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            ct
        );
        await membership.LockMemberAsync(owner, ct);
        var sub = await db.MemberSubscriptions.SingleAsync(x => x.Id == id, ct);
        sub.Cancelled = true;
        membership.Audit(
            User.Identity?.Name ?? "Admin",
            "Subscription.Stop",
            $"Member {owner}; subscription {id}; {reason}"
        );
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return RedirectToAction(nameof(Index), new { q = owner });
    }
}
