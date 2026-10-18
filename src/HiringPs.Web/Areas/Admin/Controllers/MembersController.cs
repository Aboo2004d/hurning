using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class MembersController(
    HiringDb db,
    MembershipService membership,
    MemberVerification verification,
    SiteSeo seo
) : Controller
{
    public async Task<IActionResult> Index(
        string? q,
        bool? suspended,
        bool? linked,
        int? planId,
        int page = 1,
        CancellationToken ct = default
    )
    {
        var query = db.MemberProfiles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x =>
                x.Name.Contains(q)
                || x.Mobile.Contains(q)
                || db.Users.Any(u => u.Id == x.UserId && u.Email!.Contains(q))
            );
        if (suspended is not null)
            query = query.Where(x => x.Suspended == suspended);
        if (linked is not null)
            query = query.Where(x => (x.TelegramUserId != null) == linked);
        if (planId is not null)
            query = query.Where(x =>
                db.MemberSubscriptions.Any(s =>
                    s.UserId == x.UserId
                    && !s.Cancelled
                    && s.StartsAt <= DateTimeOffset.UtcNow
                    && s.EndsAt > DateTimeOffset.UtcNow
                    && s.PlanVersion.PlanId == planId
                )
            );
        return View(
            await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.UserId)
                .PageAsync(page, ct)
        );
    }

    public async Task<IActionResult> Details(string id, CancellationToken ct)
    {
        var member = await db
            .MemberProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == id, ct);
        if (member is null)
            return NotFound();
        ViewBag.Email = await db.Users.Where(x => x.Id == id).Select(x => x.Email).SingleAsync(ct);
        ViewBag.Audit = await db
            .Audit.AsNoTracking()
            .Where(x => x.Detail.Contains(id))
            .OrderByDescending(x => x.At)
            .ThenByDescending(x => x.Id)
            .PageAsync(int.TryParse(Request.Query["page"], out var page) ? page : 1, ct);
        return View(member);
    }

    [HttpPost]
    public async Task<IActionResult> Suspend(
        string id,
        bool suspended,
        bool freeze,
        string reason,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1500)
            return BadRequest();
        await membership.SuspendAsync(
            id,
            suspended,
            freeze,
            reason,
            User.Identity?.Name ?? "Admin",
            ct
        );
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Recover(
        string id,
        string mobile,
        string reason,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1500)
            return BadRequest();
        var member = await db.MemberProfiles.SingleOrDefaultAsync(x => x.UserId == id, ct);
        if (member is null)
            return NotFound();
        member.Mobile = MembershipRules.Mobile(mobile);
        member.TelegramUserId = null;
        member.TelegramChatId = null;
        member.Activated = false;
        await db
            .MemberChallenges.Where(x => x.UserId == id && x.ConsumedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.ConsumedAt, DateTimeOffset.UtcNow), ct);
        membership.Audit(
            User.Identity?.Name ?? "Admin",
            "Member.Recover",
            $"Member {id}; identity verified by administrator; {reason}"
        );
        await db.SaveChangesAsync(ct);
        try
        {
            var request = await verification.CreateAsync(
                id,
                ChallengePurpose.Registration,
                null,
                ct
            );
            TempData["RecoveryLink"] =
                seo.Origin
                + Url.Action(
                    "Recover",
                    "Registration",
                    new
                    {
                        area = "",
                        id = request.Id,
                        token = request.Token,
                    }
                );
        }
        catch (InvalidOperationException ex)
        {
            TempData["Notice"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
