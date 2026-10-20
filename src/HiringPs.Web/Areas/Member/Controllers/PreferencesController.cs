using System.Text.Json;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Member.Controllers;

public class PreferencesController(HiringDb db) : MemberControllerBase(db)
{
    private async Task FillAsync(PreferenceInput input, CancellationToken ct)
    {
        input.AvailableCategories = await Db
            .Opportunities.Where(x =>
                x.Category != ""
                && (
                    x.Status == PublicationStatus.Published
                    || x.Status == PublicationStatus.Archived
                )
            )
            .Select(x => x.Category)
            .Distinct()
            .OrderBy(x => x)
            .ToArrayAsync(ct);
        input.AvailableLocations = (await LocationCatalog.ApprovedAsync(Db, ct))
            .Concat(CityCatalog.ArabicNames)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
    }

    /// <summary>
    /// "Alert me for this search": merges the listing's kind, city and category into the
    /// member's existing alert preferences, using the same catalogs as the preferences page.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddSearch(
        OpportunityKind? kind,
        string? city,
        string? category,
        string? returnUrl,
        CancellationToken ct
    )
    {
        if (
            (kind is null && string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(category))
            || (kind is { } requested && !Enum.IsDefined(requested))
        )
            return BadRequest();
        var pref = await Db.MemberPreferences.SingleAsync(x => x.UserId == MemberId, ct);
        var input = new PreferenceInput();
        await FillAsync(input, ct);
        var categories = JsonSerializer.Deserialize<string[]>(pref.CategoriesJson) ?? [];
        var locations = JsonSerializer.Deserialize<string[]>(pref.LocationsJson) ?? [];
        if (!string.IsNullOrWhiteSpace(category))
        {
            // Equivalent spellings share one listing filter; keep all that the catalog offers.
            var added = FilterLabels
                .Equivalents(category, input.AvailableCategories, FilterLabels.Category)
                .Where(input.AvailableCategories.Contains)
                .ToArray();
            if (added.Length == 0)
                return BadRequest();
            categories = categories.Concat(added).Distinct().ToArray();
        }
        if (!string.IsNullOrWhiteSpace(city))
        {
            var location = input.AvailableLocations.Contains(city)
                ? city
                : CityCatalog.Find(city)?.Arabic;
            if (location is null || !input.AvailableLocations.Contains(location))
                return BadRequest();
            locations = locations.Append(location).Distinct().ToArray();
        }
        if (kind is { } selected)
            pref.Kinds |= MembershipRules.Bit(selected);
        pref.CategoriesJson = JsonSerializer.Serialize(categories);
        pref.LocationsJson = JsonSerializer.Serialize(locations);
        pref.ChangedAt = DateTimeOffset.UtcNow;
        await Db.SaveChangesAsync(ct);
        Response.Headers.CacheControl = "no-store";
        if (Request.Headers["X-Member-Async"] == "true")
            return Json(new { saved = true });
        TempData["Notice"] = "AlertSearchSaved";
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var pref = await Db.MemberPreferences.SingleAsync(x => x.UserId == MemberId, ct);
        var input = new PreferenceInput
        {
            Kinds = Enum.GetValues<OpportunityKind>()
                .Where(x => MembershipRules.Includes(pref.Kinds, x))
                .ToArray(),
            Categories = JsonSerializer.Deserialize<string[]>(pref.CategoriesJson) ?? [],
            Locations = JsonSerializer.Deserialize<string[]>(pref.LocationsJson) ?? [],
            SavedReminders = Profile.SavedRemindersEnabled,
        };
        await FillAsync(input, ct);
        Response.Headers.CacheControl = "no-store";
        return View(input);
    }

    [HttpPost]
    public async Task<IActionResult> Index(
        PreferenceInput input,
        string? addCategory,
        string? removeCategory,
        string? addLocation,
        string? removeLocation,
        CancellationToken ct,
        string? bulkAction = null
    )
    {
        var pref = await Db.MemberPreferences.SingleAsync(x => x.UserId == MemberId, ct);
        var priorCategories = JsonSerializer.Deserialize<string[]>(pref.CategoriesJson) ?? [];
        var priorLocations = JsonSerializer.Deserialize<string[]>(pref.LocationsJson) ?? [];
        await FillAsync(input, ct);
        if (
            new[] { addCategory, removeCategory, addLocation, removeLocation, bulkAction }.Count(
                x => x is not null
            ) > 1
        )
            return BadRequest();
        if (addCategory is not null)
        {
            if (!input.AvailableCategories.Contains(addCategory))
                return BadRequest();
            input.Categories = input.Categories.Append(addCategory).Distinct().ToArray();
        }
        if (removeCategory is not null)
            input.Categories = input.Categories.Where(x => x != removeCategory).ToArray();
        if (addLocation is not null)
        {
            if (!input.AvailableLocations.Contains(addLocation))
                return BadRequest();
            input.Locations = input.Locations.Append(addLocation).Distinct().ToArray();
        }
        if (removeLocation is not null)
            input.Locations = input.Locations.Where(x => x != removeLocation).ToArray();
        switch (bulkAction)
        {
            case null:
                break;
            case "select-categories":
                input.Categories = input
                    .Categories.Concat(input.AvailableCategories)
                    .Distinct()
                    .ToArray();
                break;
            case "clear-categories":
                input.Categories = [];
                break;
            case "select-locations":
                input.Locations = input
                    .Locations.Concat(input.AvailableLocations)
                    .Distinct()
                    .ToArray();
                break;
            case "clear-locations":
                input.Locations = [];
                break;
            default:
                return BadRequest();
        }
        // Keep previously chosen values removable even if the source catalog no longer lists them.
        if (
            !ModelState.IsValid
            || input.Kinds.Any(x => !Enum.IsDefined(x))
            || input.Categories.Except(input.AvailableCategories.Concat(priorCategories)).Any()
            || input.Locations.Except(input.AvailableLocations.Concat(priorLocations)).Any()
        )
            return BadRequest();
        input.Categories = input.Categories.Distinct().ToArray();
        input.Locations = input.Locations.Distinct().ToArray();
        var categories = JsonSerializer.Serialize(input.Categories);
        var locations = JsonSerializer.Serialize(input.Locations);
        pref.Kinds = input.Kinds.Aggregate(0, (mask, kind) => mask | MembershipRules.Bit(kind));
        pref.CategoriesJson = categories;
        pref.LocationsJson = locations;
        pref.ChangedAt = DateTimeOffset.UtcNow;
        Profile.SavedRemindersEnabled = input.SavedReminders;
        await Db.SaveChangesAsync(ct);
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
