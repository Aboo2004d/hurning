using System.Globalization;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Member.Controllers;

public class SavedController(HiringDb db, MembershipService membership, SavedOpportunityService saved) : MemberControllerBase(db)
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var model = await saved.ForMember(MemberId).PageAsync(page, ct);
        ViewBag.AllowedDetails = await membership.DetailKindsAsync(MemberId, false, ct);
        Response.Headers.CacheControl = "no-store";
        return Request.Headers["X-Member-Async"] == "true"
            ? PartialView("_SavedResults", model)
            : View("Index", model);
    }

    [HttpPost]
    public async Task<IActionResult> Add(long id, string? returnUrl, CancellationToken ct)
    {
        if (!await saved.AddAsync(MemberId, id, ct))
            return NotFound();
        Response.Headers.CacheControl = "no-store";
        if (Request.Headers["X-Member-Async"] == "true")
            return Json(
                new
                {
                    saved = true,
                    opportunityId = id.ToString(CultureInfo.InvariantCulture)
                }
            );
        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl!)
            : RedirectToAction(nameof(Index));
    }

    // Card actions use the opportunity ID; the saved-list Remove action uses a saved-row ID.
    [HttpPost]
    public async Task<IActionResult> Unsave(long id, string? returnUrl, CancellationToken ct)
    {
        await saved.UnsaveAsync(MemberId, id, ct);
        Response.Headers.CacheControl = "no-store";
        if (Request.Headers["X-Member-Async"] == "true")
            return Json(
                new
                {
                    saved = false,
                    opportunityId = id.ToString(CultureInfo.InvariantCulture)
                }
            );
        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl!)
            : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Remove(long id, int page = 1, CancellationToken ct = default)
    {
        await saved.RemoveAsync(MemberId, id, ct);
        return Request.Headers["X-Member-Async"] == "true"
            ? await Index(page, ct)
            : RedirectToAction(nameof(Index), new
            {
                page
            });
    }
}
