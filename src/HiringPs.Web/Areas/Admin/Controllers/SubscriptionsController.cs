using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin")]
public class SubscriptionsController(HiringDb db, MembershipService membership, SubscriptionReadService? readService = null, SubscriptionManagementService? managementService = null) : Controller
{
    public async Task<IActionResult> Index(
        string? q,
        string? status,
        int page = 1,
        CancellationToken ct = default
    )
    {
        var reader = readService ?? new SubscriptionReadService(db);
        ViewBag.Plans = await reader.CurrentPlansAsync(ct);
        return View(await reader.AdminSubscriptions(q, status).PageAsync(page, ct));
    }

    /// <summary>يحل البريد ويعرض اسم صاحب الحساب قبل أي تغيير في الاشتراك.</summary>
    [HttpPost]
    public async Task<IActionResult> PreviewGrant(SubscriptionGrantInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(input.Reason)) return BadRequest();
        var member = await new AdminMemberLookupService(db).ByEmailAsync(input.Email, ct);
        if (member is null)
        {
            TempData["Notice"] = "MemberEmailNotFound";
            return RedirectToAction(nameof(Index), new { q = input.Email });
        }
        var reader = readService ?? new SubscriptionReadService(db);
        var plan = (await reader.CurrentPlansAsync(ct)).SingleOrDefault(x => x.Id == input.VersionId);
        if (plan is null) return BadRequest();
        input.Email = member.Email;
        Response.Headers.CacheControl = "no-store";
        return View("ConfirmGrant", new SubscriptionGrantConfirmation(input, member, plan));
    }

    /// <summary>يعيد حل البريد ويتحقق من ثبات الحساب الذي عُرض للتأكيد قبل منح الاشتراك.</summary>
    [HttpPost]
    public async Task<IActionResult> Grant(SubscriptionGrantInput input, string confirmedUserId, CancellationToken ct)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(input.Reason)) return BadRequest();
        var member = await new AdminMemberLookupService(db).ByEmailAsync(input.Email, ct);
        if (member is null || member.UserId != confirmedUserId)
        {
            TempData["Notice"] = "MemberEmailConfirmationChanged";
            return RedirectToAction(nameof(Index), new { q = input.Email });
        }
        var reader = readService ?? new SubscriptionReadService(db);
        if (!(await reader.CurrentPlansAsync(ct)).Any(x => x.Id == input.VersionId)) return BadRequest();
        await membership.GrantAsync(member.UserId, input.VersionId, input.Days,
            User.Identity?.Name ?? "Admin", input.Reason, ct);
        return RedirectToAction(nameof(Index), new { q = member.Email });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(long id, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1500)
            return BadRequest();
        var manager = managementService ?? new SubscriptionManagementService(db, membership);
        var owner = await manager.CancelAsync(id, reason, User.Identity?.Name ?? "Admin", ct);
        return RedirectToAction(nameof(Index), new { q = await new AdminMemberLookupService(db).EmailAsync(owner, ct) });
    }
}
