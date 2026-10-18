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
            .Concat(
                new[]
                {
                    "رام الله",
                    "غزة",
                    "القدس",
                    "الخليل",
                    "نابلس",
                    "جنين",
                    "بيت لحم",
                    "أريحا",
                    "طولكرم",
                    "قلقيلية",
                    "طوباس",
                    "سلفيت",
                }
            )
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
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
        return View(input);
    }

    [HttpPost]
    public async Task<IActionResult> Index(PreferenceInput input, CancellationToken ct)
    {
        await FillAsync(input, ct);
        if (
            input.Kinds.Any(x => !Enum.IsDefined(x))
            || input.Categories.Except(input.AvailableCategories).Any()
            || input.Locations.Except(input.AvailableLocations).Any()
        )
            return BadRequest();
        var pref = await Db.MemberPreferences.SingleAsync(x => x.UserId == MemberId, ct);
        pref.Kinds = input.Kinds.Aggregate(0, (mask, kind) => mask | MembershipRules.Bit(kind));
        pref.CategoriesJson = JsonSerializer.Serialize(input.Categories.Distinct());
        pref.LocationsJson = JsonSerializer.Serialize(input.Locations.Distinct());
        if (pref.CategoriesJson.Length > 4000 || pref.LocationsJson.Length > 4000)
            return BadRequest();
        pref.ChangedAt = DateTimeOffset.UtcNow;
        Profile.SavedRemindersEnabled = input.SavedReminders;
        await Db.SaveChangesAsync(ct);
        TempData["Notice"] = "SavedChanges";
        return RedirectToAction(nameof(Index));
    }
}
