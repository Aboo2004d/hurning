using HiringPs.Core;
using HiringPs.Web.Models;
using HiringPs.Web.Services.Membership;
using Microsoft.AspNetCore.Mvc;

namespace HiringPs.Web.Areas.Member.Controllers;

public class PreferencesController(HiringDb db, MemberPreferencePageService preferences) : MemberControllerBase(db)
{
    /// <summary>يحفظ بحث القائمة كتفضيلات تنبيه بعد التحقق من كتالوج الخيارات.</summary>
    [HttpPost]
    public async Task<IActionResult> AddSearch(OpportunityKind? kind, string? city, string? category, string? returnUrl, CancellationToken ct)
    {
        if (!await preferences.AddSearchAsync(MemberId, kind, city, category, ct))
            return BadRequest();
        Response.Headers.CacheControl = "no-store";
        if (Request.Headers["X-Member-Async"] == "true")
            return Json(new
            {
                saved = true
            });
        TempData["Notice"] = "AlertSearchSaved";
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var input = await preferences.ReadAsync(MemberId, Profile.SavedRemindersEnabled, ct);
        Response.Headers.CacheControl = "no-store";
        return View(input);
    }

    [HttpPost]
    public async Task<IActionResult> Index(PreferenceInput input, string? addCategory, string? removeCategory,
        string? addLocation, string? removeLocation, CancellationToken ct, string? bulkAction = null)
    {
        if (!ModelState.IsValid || !await preferences.EditAsync(MemberId, input, addCategory, removeCategory,
            addLocation, removeLocation, bulkAction, ct))
            return BadRequest();
        Response.Headers.CacheControl = "no-store";
        if (Request.Headers["X-Member-Async"] == "true")
        {
            ModelState.Clear();
            ViewData["PreferenceNotice"] = "SavedChanges";
            return PartialView("_PreferenceEditor", input);
        }
        TempData["Notice"] = "SavedChanges";
        return RedirectToAction(nameof(Index));
    }
}
