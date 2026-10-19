using System.Security.Claims;
using System.Text.Json;
using HiringPs.Core;
using HiringPs.Web.Areas.Member.Controllers;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

// Optional persistence checks in the disposable membership SQL database.
public static class MemberWorkspaceSqlChecks
{
    public static async Task RunAsync(DbContextOptions<HiringDb> options)
    {
        void Check(bool value, string label)
        {
            if (!value)
                throw new Exception("FAIL: " + label);
            Console.WriteLine("PASS: " + label);
        }
        var owner = Guid.NewGuid().ToString();
        var other = Guid.NewGuid().ToString();
        var category = "workspace-category-" + Guid.NewGuid().ToString("N");
        long opportunityId;
        await using (var db = new HiringDb(options))
        {
            foreach (var id in new[] { owner, other })
            {
                db.Users.Add(new IdentityUser { Id = id, UserName = id + "@invalid.test" });
                db.MemberProfiles.Add(
                    new MemberProfile
                    {
                        UserId = id,
                        Name = "Fixture",
                        Activated = true,
                    }
                );
                db.MemberPreferences.Add(
                    new MemberPreference
                    {
                        UserId = id,
                        CategoriesJson = "[\"legacy-category\"]",
                        LocationsJson = "[\"رام الله\"]",
                    }
                );
            }
            var opportunity = new Opportunity
            {
                Title = "Saved fixture",
                Organization = "Fixture",
                Category = category,
                Status = PublicationStatus.Published,
            };
            db.Opportunities.Add(opportunity);
            await db.SaveChangesAsync();
            opportunityId = opportunity.Id;
        }
        async Task<IActionResult> Save()
        {
            await using var db = new HiringDb(options);
            var controller = Setup(new SavedController(db, new MembershipService(db)), owner);
            return await controller.Add(opportunityId, null, default);
        }
        var results = await Task.WhenAll(Save(), Save());
        Check(
            results.All(x => x is JsonResult),
            "Concurrent save requests both return confirmed saved state"
        );
        long savedId;
        await using (var db = new HiringDb(options))
        {
            var saved = await db
                .SavedOpportunities.Where(x =>
                    x.UserId == owner && x.OpportunityId == opportunityId
                )
                .ToListAsync();
            Check(
                saved.Count == 1,
                "Concurrent saves persist one bookmark per member and opportunity"
            );
            savedId = saved.Single().Id;
            var wrongOwner = Setup(new SavedController(db, new MembershipService(db)), other);
            await wrongOwner.Remove(savedId);
            Check(
                await db.SavedOpportunities.AnyAsync(x => x.Id == savedId),
                "Removing another member's saved row cannot delete it"
            );
        }
        await using (var db = new HiringDb(options))
        {
            await Setup(new SavedController(db, new MembershipService(db)), owner).Remove(savedId);
            Check(
                !await db.SavedOpportunities.AnyAsync(x => x.Id == savedId),
                "Saved removal persists for its owner"
            );
        }

        async Task<IActionResult?> Change(
            string? addCategory = null,
            string? removeCategory = null,
            string? addLocation = null,
            string? removeLocation = null
        )
        {
            await using var db = new HiringDb(options);
            var pref = await db
                .MemberPreferences.AsNoTracking()
                .SingleAsync(x => x.UserId == owner);
            var input = new PreferenceInput
            {
                Kinds = [OpportunityKind.Job],
                Categories = JsonSerializer.Deserialize<string[]>(pref.CategoriesJson)!,
                Locations = JsonSerializer.Deserialize<string[]>(pref.LocationsJson)!,
            };
            var controller = Setup(new PreferencesController(db), owner);
            var action = new ActionContext(
                controller.HttpContext,
                new RouteData(),
                new ControllerActionDescriptor()
            );
            IActionResult? result = null;
            await controller.OnActionExecutionAsync(
                new ActionExecutingContext(
                    action,
                    [],
                    new Dictionary<string, object?>(),
                    controller
                ),
                async () =>
                {
                    result = await controller.Index(
                        input,
                        addCategory,
                        removeCategory,
                        addLocation,
                        removeLocation,
                        default
                    );
                    return new ActionExecutedContext(action, [], controller) { Result = result };
                }
            );
            return result;
        }
        Check(
            await Change(addLocation: "غزة") is PartialViewResult,
            "Location can be added while retaining an old category missing from the catalog"
        );
        Check(
            await Change(removeCategory: "legacy-category") is PartialViewResult,
            "Legacy selected category remains removable"
        );
        await using (var db = new HiringDb(options))
            Check(
                (await db.MemberPreferences.SingleAsync(x => x.UserId == owner)).CategoriesJson
                    == "[]",
                "Removing the last category persists all-category preference semantics"
            );
        Check(
            await Change(addCategory: category) is PartialViewResult,
            "A currently available category can be selected"
        );
        Check(
            await Change(addCategory: "not-in-the-source-catalog") is BadRequestResult,
            "A forged category is rejected by the server"
        );
        Check(
            await Change(removeLocation: "غزة") is PartialViewResult,
            "Selected location can be removed"
        );
        await using (var db = new HiringDb(options))
        {
            var pref = await db.MemberPreferences.SingleAsync(x => x.UserId == owner);
            Check(
                JsonSerializer.Deserialize<string[]>(pref.CategoriesJson)!.SequenceEqual([category])
                    && JsonSerializer
                        .Deserialize<string[]>(pref.LocationsJson)!
                        .SequenceEqual(["رام الله"]),
                "Preference mutations persist only the desired categories and locations"
            );
            Check(
                (await db.MemberPreferences.SingleAsync(x => x.UserId == other)).CategoriesJson
                    == "[\"legacy-category\"]",
                "Preference changes cannot alter a different member"
            );
        }
    }

    private static T Setup<T>(T controller, string owner)
        where T : Controller
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner)], "Fixture")
            ),
        };
        http.Request.Headers["X-Member-Async"] = "true";
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new EmptyTempData());
        return controller;
    }

    private sealed class EmptyTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) =>
            new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
