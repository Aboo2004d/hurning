using HiringPs.Core;
using Microsoft.EntityFrameworkCore;

/// <summary>اختبارات اختيارية في قاعدة SQL مؤقتة للاعتماد والتعارض والحذف.</summary>
public static class CategoryGroupSqlChecks
{
    public static async Task RunAsync(DbContextOptions<HiringDb> options)
    {
        void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS: " + label); }
        var prefix = "group-fixture-" + Guid.NewGuid().ToString("N");
        var labels = new[] { prefix + "-education", prefix + "-تعليم", prefix + "-new" };
        int id;
        byte[] version;
        await using (var db = new HiringDb(options))
        {
            db.Opportunities.AddRange(labels.Select(label => new Opportunity { Category = label, Title = "Group fixture", Organization = "Fixture", Status = PublicationStatus.Published }));
            await db.SaveChangesAsync();
            var management = new CategoryGroupManagementService(db);
            Check(await management.SaveAsync(new(0, prefix, labels[..2], [], "Fixture"), default) == CategoryGroupSaveOutcome.Saved,
                "Administrator approves a group of existing names");
            var group = (await new CategoryGroupReadService(db).Groups(prefix).ToListAsync()).Single();
            id = group.Id; version = group.Version;
            var map = await new CategoryGroupReadService(db).MapAsync(default);
            Check(map.Matches(labels[0], labels[1]) && !map.Matches(labels[0], labels[2]), "Only approved members become equivalent");
            db.Users.Add(new Microsoft.AspNetCore.Identity.IdentityUser { Id = prefix, UserName = prefix + "@invalid.test" });
            db.MemberProfiles.Add(new MemberProfile { UserId = prefix, Name = "Fixture", Activated = true });
            db.MemberPreferences.Add(new MemberPreference { UserId = prefix });
            await db.SaveChangesAsync();
            var preferences = new MemberPreferenceService(db);
            Check(await preferences.SaveAsync(prefix, new(15, [labels[0], labels[1], labels[2], "Independent A"], [], null), default),
                "SQL save counts approved aliases as one within free quota");
            Check(await new HiringPs.Web.Services.Membership.MemberPreferencePageService(preferences)
                    .AddSearchAsync(prefix, OpportunityKind.Job, null, labels[1], default) == PreferenceSaveOutcome.Saved,
                "Saving a search accepts an approved alias even when catalog shows one representative");
            Check(!await preferences.SaveAsync(prefix, new(15, [labels[0], labels[1], labels[2], "Independent A", "Independent B"], [], null), default),
                "Fourth independent field rejected even with grouped aliases");
            Check(await management.SaveAsync(new(0, prefix + "-other", [labels[0]], [], "Fixture"), default) == CategoryGroupSaveOutcome.Conflict,
                "A name cannot join a second group");
            Check(!await db.CategoryGroups.AnyAsync(x => x.Name == prefix + "-other"), "Conflicting approval creates no partial group");
        }
        async Task<CategoryGroupSaveOutcome> Change(string suffix)
        {
            await using var db = new HiringDb(options);
            return await new CategoryGroupManagementService(db).SaveAsync(new(id, prefix + suffix, labels, version, "Fixture"), default);
        }
        var results = await Task.WhenAll(Change("-one"), Change("-two"));
        Check(results.Count(x => x == CategoryGroupSaveOutcome.Saved) == 1 && results.Count(x => x == CategoryGroupSaveOutcome.Conflict) == 1,
            "Concurrent group edits cannot overwrite each other");
        await using (var db = new HiringDb(options))
        {
            var reader = new CategoryGroupReadService(db);
            var group = (await reader.FindAsync(id, default))!;
            var management = new CategoryGroupManagementService(db);
            Check(await management.SaveAsync(new(id, group.Name, labels[..1], group.Version, "Fixture"), default) == CategoryGroupSaveOutcome.Saved,
                "Removing a group member is explicit and persisted");
            var map = await reader.MapAsync(default);
            Check(!map.Matches(labels[0], labels[1]), "Removed member becomes independent");
            group = (await reader.FindAsync(id, default))!;
            Check(await management.DeleteAsync(id, group.Version, "Fixture", default) == CategoryGroupSaveOutcome.Saved,
                "Group can be removed with current version");
            Check(!await db.CategoryGroupMembers.AnyAsync(x => x.CategoryGroupId == id) && await db.Opportunities.CountAsync(x => labels.Contains(x.Category)) == 3,
                "Ungrouping deletes only membership rows and preserves opportunity names");
        }
    }
}
