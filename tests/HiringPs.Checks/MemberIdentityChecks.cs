using System.ComponentModel.DataAnnotations;
using HiringPs.Core;
using HiringPs.Web.Areas.Admin.Controllers;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>يتحقق من البريد والتأكيد قبل المنح عبر استعلامات فعلية في SQLite مؤقت، دون تعديل بيانات حقيقية.</summary>
public static class MemberIdentityChecks
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new HiringDb(new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE AspNetUsers (Id TEXT PRIMARY KEY, Email TEXT, NormalizedEmail TEXT, UserName TEXT);
            CREATE TABLE MemberProfiles (UserId TEXT PRIMARY KEY, Name TEXT);
            CREATE TABLE MembershipPlans (Id INTEGER PRIMARY KEY, CurrentVersionId INTEGER);
            CREATE TABLE MembershipPlanVersions (Id INTEGER PRIMARY KEY, NameAr TEXT, NameEn TEXT);
            INSERT INTO AspNetUsers VALUES ('internal-id-a','alice@example.test','ALICE@EXAMPLE.TEST','alice@example.test');
            INSERT INTO AspNetUsers VALUES ('internal-id-b','bob@example.test','BOB@EXAMPLE.TEST','bob@example.test');
            INSERT INTO MemberProfiles VALUES ('internal-id-a','العضوة الأولى'), ('internal-id-b','العضو الثاني');
            INSERT INTO MembershipPlans VALUES (1,11);
            INSERT INTO MembershipPlanVersions VALUES (11,'باقة اختبار','Test plan');
            """);
        var lookup = new AdminMemberLookupService(db);
        var member = await lookup.ByEmailAsync("  ALICE@example.test  ", default);
        Check(member?.UserId == "internal-id-a" && member.Name == "العضوة الأولى", "Email lookup is exact, normalized and displays the correct name");
        Check(await lookup.ByEmailAsync("alice", default) is null, "Partial email is never used for granting");
        Check(await lookup.ByEmailAsync("missing@example.test", default) is null, "Unknown email is rejected");
        Check(await lookup.ByEmailAsync("internal-id-a", default) is null, "Manual IDs cannot replace email input");
        Check(await lookup.ByEmailAsync("alice@example.test' OR '1'='1", default) is null, "Invalid email cannot broaden account selection");
        var referencedNames = await lookup.ReferencedNamesAsync(["Member internal-id-a; order order-42"], default);
        var rendered = MemberReferenceDisplay.Render("Member internal-id-a; order order-42", referencedNames);
        Check(rendered.Contains("العضوة الأولى") && !rendered.Contains("internal-id-a") && rendered.Contains("order-42"),
            "Audit display resolves names without changing order references or stored audit data");
        var input = new SubscriptionGrantInput { Email = "alice@example.test", VersionId = 11, Reason = "اختبار" };
        var errors = new List<ValidationResult>();
        Check(Validator.TryValidateObject(input, new ValidationContext(input), errors, true), "Grant input validates email, plan and reason");
        input.Days = 0;
        Check(!Validator.TryValidateObject(input, new ValidationContext(input), [], true), "Invalid grant duration rejected");
        input.Days = null;
        var context = new DefaultHttpContext();
        var controller = new SubscriptionsController(db, new MembershipService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new TestTempData()),
        };
        var preview = await controller.PreviewGrant(input, default) as ViewResult;
        var confirmation = preview?.Model as SubscriptionGrantConfirmation;
        Check(preview?.ViewName == "ConfirmGrant" && confirmation?.Member.Name == "العضوة الأولى"
            && confirmation.Member.Email == "alice@example.test", "Preview shows name and email before granting");
        Check(confirmation?.Plan.Id == 11, "Preview retains the selected plan");
        var wrongAccount = await controller.Grant(input, "internal-id-b", default);
        Check(wrongAccount is RedirectToActionResult && (string?)controller.TempData["Notice"] == "MemberEmailConfirmationChanged",
            "Changing email to a different account after confirmation cannot grant a subscription");
        Check(await controller.Grant(input, "", default) is RedirectToActionResult, "Grant cannot skip account confirmation");
        await db.Database.ExecuteSqlRawAsync("UPDATE AspNetUsers SET Email='changed@example.test',NormalizedEmail='CHANGED@EXAMPLE.TEST' WHERE Id='internal-id-a';");
        Check(await controller.Grant(input, "internal-id-a", default) is RedirectToActionResult,
            "Email is resolved again at final confirmation");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO AspNetUsers VALUES ('duplicate-1','dup@example.test','DUP@EXAMPLE.TEST','dup1');
            INSERT INTO AspNetUsers VALUES ('duplicate-2','dup@example.test','DUP@EXAMPLE.TEST','dup2');
            INSERT INTO MemberProfiles VALUES ('duplicate-1','أول'), ('duplicate-2','ثاني');
            """);
        Check(await lookup.ByEmailAsync("dup@example.test", default) is null, "Duplicate normalized emails never select an arbitrary user");
        await using var sql = new HiringDb(new DbContextOptionsBuilder<HiringDb>().UseSqlServer("Server=.;Database=metadata").Options);
        var subscriptions = new SubscriptionReadService(sql).AdminSubscriptions("alice@example.test", null).ToQueryString();
        Check(subscriptions.Contains("[Email]") && subscriptions.Contains("[Name]"), "SQL subscription list projects names and searches email");
        var notifications = new NotificationAdministrationService(sql, new MembershipService(sql)).Messages("alice@example.test", null).ToQueryString();
        Check(notifications.Contains("[NormalizedEmail]") && notifications.Contains("[Name]"), "Notification filter uses email and projects member name");
        var history = new OperationReadService(sql).Query(new OperationFilterDto("alice@example.test")).ToQueryString();
        Check(history.Contains("[MemberProfiles]") && history.Contains("[Email]"), "Operation history can search member name or email");
        Check(!sql.Database.HasPendingModelChanges(), "Display and lookup changes require no migration");
        Console.WriteLine($"Member identity: {passed} checks passed; SQLite fixture only, no subscription granted.");
    }

    private sealed class TestTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
