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
        var manager = managementService ?? new SubscriptionManagementService(db, membership);
        var owner = await manager.CancelAsync(id, reason, User.Identity?.Name ?? "Admin", ct);
        return RedirectToAction(nameof(Index), new { q = owner });
    }
}
