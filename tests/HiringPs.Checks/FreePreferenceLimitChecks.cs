using HiringPs.Core;
using HiringPs.Web.Areas.Admin.Controllers;
using HiringPs.Web.Services.Membership;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Security.Claims;

internal static class FreePreferenceLimitChecks
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); count++; Console.WriteLine("PASS " + label); }
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        connection.CreateCollation("Latin1_General_100_BIN2", string.CompareOrdinal);
        using var db = new FixtureDb(new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection).Options);
        var tables = new[] { typeof(MemberProfile), typeof(MemberPreference), typeof(MemberPreferenceSetting), typeof(MemberSubscription),
            typeof(MembershipPlanVersion), typeof(CategoryGroup), typeof(CategoryGroupMember), typeof(AuditEntry), typeof(Opportunity) }
            .Select(type => db.Model.FindEntityType(type)!.GetTableName()).ToArray();
        foreach (var sql in db.Database.GenerateCreateScript().Split(';', StringSplitOptions.RemoveEmptyEntries))
            if (tables.Any(table => sql.TrimStart().StartsWith($"CREATE TABLE \"{table}\""))) await db.Database.ExecuteSqlRawAsync(sql);
        var now = DateTimeOffset.UtcNow;
        var paid = new MembershipPlanVersion { PriceIls = 20, MaxPreferredCategories = 5 };
        var unlimited = new MembershipPlanVersion { PriceIls = 40, MaxPreferredCategories = null };
        db.MembershipPlanVersions.AddRange(paid, unlimited);
        foreach (var id in new[] { "free", "paid", "expired", "future", "cancelled", "unlimited" })
        {
            db.MemberProfiles.Add(new() { UserId = id, Name = id });
            db.MemberPreferences.Add(new() { UserId = id });
            if (id != "free") db.MemberSubscriptions.Add(new()
            {
                UserId = id, PlanVersion = id == "unlimited" ? unlimited : paid,
                StartsAt = id == "future" ? now.AddDays(1) : now.AddDays(-10),
                EndsAt = id == "expired" ? now.AddDays(-1) : now.AddDays(10), Cancelled = id == "cancelled",
            });
        }
        await db.SaveChangesAsync();
        var settings = new MemberPreferenceSettingsService(db);
        var preferences = new MemberPreferenceService(db);
        Check(await settings.ReadAsync(default) == 3 && await preferences.LimitAsync("free", default) == 3,
            "Existing databases retain the default free quota of three before an admin saves settings");
        Check(await settings.SaveAsync(1, "Fixture admin", default), "Administrator can persist a new non-subscriber quota");
        foreach (var id in new[] { "free", "expired", "future", "cancelled" })
            Check(await preferences.LimitAsync(id, default) == 1, "Configured free quota applies to " + id);
        Check(await preferences.LimitAsync("paid", default) == 5 && await preferences.LimitAsync("unlimited", default) is null,
            "Purchased finite and unlimited plan limits are independent of the free quota");
        Check(!await preferences.SaveAsync("free", new(15, ["Accounting", "IT"], [], null), default),
            "Server rejects direct saves exceeding the configured free quota");
        Check(await preferences.SaveAsync("free", new(15, ["Accounting"], [], null), default), "Exact free quota can be saved");
        Check(await settings.SaveAsync(0, "Fixture admin", default), "Zero can disable category selection for free accounts");
        Check((await preferences.ReadAsync("free", default)).Categories.SequenceEqual(new[] { "Accounting" }),
            "Reducing the quota does not delete existing preferences");
        Check(!await preferences.SaveAsync("free", new(15, ["Accounting"], [], null), default),
            "An old selection over the reduced quota cannot bypass validation");
        var page = await new MemberPreferencePageService(preferences).ReadAsync("free", true, default);
        Check(page.CategoryLimit == 0 && page.Categories.SequenceEqual(new[] { "Accounting" }),
            "Member editor shows the current quota while preserving old removable selections");
        Check(await preferences.SaveAsync("free", new(15, [], [], null), default)
            && !PreferenceLimitRules.AllowsAlerts([], 0), "Clearing selections is allowed at quota zero without enabling wildcard alerts");
        Check(!await settings.SaveAsync(-1, "Fixture", default) && !await settings.SaveAsync(1001, "Fixture", default)
            && await settings.ReadAsync(default) == 0, "Out-of-range admin values cannot change persisted settings");
        Check(await settings.SaveAsync(1000, "Fixture", default) && await preferences.LimitAsync("free", default) == 1000,
            "Upper supported quota can be stored and read");
        Check(await preferences.LimitAsync("paid", default) == 5 && paid.MaxPreferredCategories == 5 && unlimited.MaxPreferredCategories is null,
            "Changing free settings never modifies purchased plan versions");
        Check(await db.Audit.AnyAsync(x => x.Action == "Membership.FreePreferenceLimit" && x.Actor == "Fixture admin"
            && x.Detail.Contains("Previous 3; New 1")), "Quota changes record actor and previous/new values");
        using var providers = new ServiceCollection().AddLogging().AddLocalization(x => x.ResourcesPath = "Resources").BuildServiceProvider();
        var controller = new PreferenceSettingsController(db, providers.GetRequiredService<IStringLocalizer<HiringPs.Web.Labels>>())
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, "Admin action") }, "Fixture")) } },
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, new EmptyTempData());
        Check(await controller.Index(new MemberPreferenceSetting { NonSubscriberCategoryLimit = -1 }, default) is ViewResult
            && await settings.ReadAsync(default) == 1000, "Invalid admin form redisplays validation without saving");
        controller.ModelState.Clear();
        Check(await controller.Index(new MemberPreferenceSetting { NonSubscriberCategoryLimit = 2 }, default) is RedirectToActionResult
            && await settings.ReadAsync(default) == 2, "Valid admin action saves the setting and redirects");
        Check(typeof(PreferenceSettingsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles == "Admin",
            "Preference settings are restricted to administrators");
        Check(typeof(PreferenceSettingsController).GetMethods().Single(method => method.Name == "Index" && method.IsDefined(typeof(HttpPostAttribute), true))
            .IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true), "Settings changes require antiforgery verification");
        using var shape = new HiringDb(new DbContextOptionsBuilder<HiringDb>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Shape;Trusted_Connection=True").Options);
        Check(!shape.Database.HasPendingModelChanges(), "SQL Server migration snapshot matches the free preference setting");
        Console.WriteLine($"Free preference limits: {count} checks passed; isolated SQLite, no external requests.");
    }
    private sealed class EmptyTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private sealed class FixtureDb(DbContextOptions<HiringDb> options) : HiringDb(options)
    {
        protected override void OnModelCreating(ModelBuilder model)
        {
            base.OnModelCreating(model);
            foreach (var entity in model.Model.GetEntityTypes()) foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?)) property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
                if (property.Name == "Version" && property.ClrType == typeof(byte[])) property.ValueGenerated = ValueGenerated.Never;
            }
        }
    }
}
