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
    SiteSeo seo,
    AdminMemberReadService? readService = null,
    MemberRecoveryService? recoveryService = null
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
        var reader = readService ?? new AdminMemberReadService(db);
        return View(await reader.Members(q, suspended, linked, planId).PageAsync(page, ct));
    }

    public async Task<IActionResult> Details(string id, CancellationToken ct)
    {
        var reader = readService ?? new AdminMemberReadService(db);
        var member = await reader.MemberAsync(id, ct);
        if (member is null) return NotFound();
        ViewBag.Email = await reader.EmailAsync(id, ct);
        var audit = await reader.Audit(id)
            .PageAsync(int.TryParse(Request.Query["page"], out var page) ? page : 1, ct);
        var names = await new AdminMemberLookupService(db).ReferencedNamesAsync(audit.Items.Select(x => x.Detail), ct);
        for (var i = 0; i < audit.Items.Count; i++)
            audit.Items[i] = audit.Items[i] with { Detail = MemberReferenceDisplay.Render(audit.Items[i].Detail, names) };
        ViewBag.Audit = audit;
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
        var service = recoveryService ?? new MemberRecoveryService(db, membership, verification);
        var result = await service.PrepareAsync(id, mobile, reason, User.Identity?.Name ?? "Admin", ct);
        if (result.Outcome == MemberRecoveryOutcome.NotFound) return NotFound();
        if (result.Outcome == MemberRecoveryOutcome.InvalidRequest) return BadRequest();
        if (result.Notice is not null) TempData["Notice"] = result.Notice;
        if (result.Request is { } request)
            TempData["RecoveryLink"] = seo.Origin + Url.Action("Recover", "Registration",
                new { area = "", id = request.Id, token = request.Token });
        return RedirectToAction(nameof(Details), new { id });
    }
}
