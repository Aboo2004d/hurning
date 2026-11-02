using HiringPs.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
namespace HiringPs.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PreferenceSettingsController(HiringDb db, IStringLocalizer<Labels> labels) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(new MemberPreferenceSetting
        { NonSubscriberCategoryLimit = await new MemberPreferenceSettingsService(db).ReadAsync(ct) });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([Bind("NonSubscriberCategoryLimit")] MemberPreferenceSetting input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input.NonSubscriberCategoryLimit is < 0 or > 1000)
        {
            if (ModelState.TryGetValue(nameof(input.NonSubscriberCategoryLimit), out var field)) field.Errors.Clear();
            ModelState.AddModelError(nameof(input.NonSubscriberCategoryLimit), labels["FreePreferenceLimitRange"]);
            return View(input);
        }
        await new MemberPreferenceSettingsService(db).SaveAsync(input.NonSubscriberCategoryLimit, User.Identity?.Name ?? "Admin", ct);
        TempData["Notice"] = "SavedChanges";
        return RedirectToAction(nameof(Index));
    }
}
