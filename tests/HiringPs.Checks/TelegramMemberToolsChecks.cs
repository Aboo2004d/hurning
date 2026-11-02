using HiringPs.Core;
using HiringPs.Web.Areas.Admin.Controllers;
using HiringPs.Web.Models;
using HiringPs.Web.Services.Membership;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class TelegramMemberToolsChecks
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Seo:PublicOrigin"] = "https://fixture.invalid" }).Build();
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await connection.OpenAsync();
        connection.CreateCollation("Latin1_General_100_BIN2", string.CompareOrdinal);
        using var db = new FixtureDb(new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection).Options);
        var tables = new[] { typeof(IdentityUser), typeof(MemberProfile), typeof(MemberPreference), typeof(MemberSubscription), typeof(MembershipPlanVersion),
            typeof(MemberMessage), typeof(TelegramInbox), typeof(MembershipRuntimeState), typeof(TelegramMemberSetting), typeof(TelegramMemberBroadcast), typeof(AuditEntry), typeof(Opportunity),
            typeof(OpportunityPublication), typeof(IndexNowEntry), typeof(CategoryGroup), typeof(CategoryGroupMember) }
            .Select(t => db.Model.FindEntityType(t)!.GetTableName()).ToArray();
        foreach (var sql in db.Database.GenerateCreateScript().Split(';', StringSplitOptions.RemoveEmptyEntries))
            if (tables.Any(t => sql.TrimStart().StartsWith($"CREATE TABLE \"{t}\"")) || sql.Contains("IX_MemberMessages_UserId_EventKey"))
                await db.Database.ExecuteSqlRawAsync(sql);
        var now = DateTimeOffset.UtcNow;
        var profiles = new[] { "paid", "free", "expired", "future", "cancelled", "suspended", "unlinked", "inactive", "stopped", "zero" }
            .Select((id, i) => new MemberProfile { UserId = id, Name = "Member " + id, Activated = id != "inactive",
                Suspended = id == "suspended", NotificationsEnabled = id != "stopped", CreatedAt = now.AddDays(-10),
                TelegramUserId = id == "unlinked" ? null : i + 100, TelegramChatId = id == "unlinked" ? null : i + 100 }).ToArray();
        db.Users.AddRange(profiles.Select(x => new IdentityUser { Id = x.UserId }));
        db.MemberProfiles.AddRange(profiles);
        db.MemberPreferences.AddRange(profiles.Select(x => new MemberPreference { UserId = x.UserId }));
        var plan = new MembershipPlanVersion { PriceIls = 20, NameAr = "باقة", NameEn = "Plan", MaxPreferredCategories = 2 };
        var zero = new MembershipPlanVersion { PriceIls = 0 };
        db.MembershipPlanVersions.AddRange(plan, zero);
        foreach (var member in profiles.Where(x => x.UserId != "free"))
            db.MemberSubscriptions.Add(new() { UserId = member.UserId, PlanVersion = member.UserId == "zero" ? zero : plan,
                StartsAt = member.UserId == "future" ? now.AddDays(1) : now.AddDays(-5),
                EndsAt = member.UserId == "expired" ? now.AddDays(-1) : now.AddDays(5), Cancelled = member.UserId == "cancelled" });
        for (var i = 0; i < 7; i++) db.Opportunities.Add(new() { Title = "Current job " + i, Organization = "Fixture Company", Locations = "الخليل", Category = "Accounting", Status = PublicationStatus.Published, CreatedAt = now.AddMinutes(-i) });
        db.Opportunities.AddRange(new Opportunity { Title = "Expired job", Organization = "Fixture Company", Locations = "الخليل", Category = "Accounting", Status = PublicationStatus.Published, Deadline = TextRules.PalestineToday().AddDays(-1) },
            new Opportunity { Title = "Draft job", Organization = "Fixture Company", Locations = "الخليل", Status = PublicationStatus.Draft },
            new Opportunity { Title = "Archived job", Organization = "Fixture Company", Locations = "الخليل", Status = PublicationStatus.Archived },
            new Opportunity { Title = "Training", Organization = "Fixture Company", Locations = "الخليل", Kind = OpportunityKind.Training, Status = PublicationStatus.Published });
        db.CategoryGroups.Add(new CategoryGroup { Name = "Finance", NormalizedName = "finance", Members = [new CategoryGroupMember { Label = "Accounting", NormalizedKey = CategoryGroupMap.Key("Accounting") }, new CategoryGroupMember { Label = "محاسبة", NormalizedKey = CategoryGroupMap.Key("محاسبة") }] });
        await db.SaveChangesAsync();
        var search = new TelegramJobSearch(db, config);
        Check((await search.ReplyAsync("paid", "city Hebron", default)).Contains("متوقف"), "Search defaults to disabled");
        var settings = new TelegramMemberSetting { SearchEnabled = true };
        db.TelegramMemberSettings.Add(settings); await db.SaveChangesAsync();
        foreach (var id in new[] { "free", "expired", "future", "cancelled", "zero" })
            Check((await search.ReplyAsync(id, "city Hebron", default)).Contains("اشتراكًا"), "Paid audience rejects " + id);
        foreach (var id in new[] { "suspended", "unlinked", "inactive", "missing" })
            Check((await search.ReplyAsync(id, "city Hebron", default)).Contains("فعّله"), "Account gate rejects " + id);
        var reply = await search.ReplyAsync("paid", "city Hebron", default);
        Check(reply.Contains("Current job 0") && !reply.Contains("Current job 5") && reply.Contains("| 2"), "City aliases work with five-result pagination");
        Check(!reply.Contains("Expired job") && !reply.Contains("Draft job") && !reply.Contains("Archived job") && !reply.Contains("Training"), "Only current published jobs appear");
        Check((await search.ReplyAsync("paid", "city الخليل | 2", default)).Contains("Current job 6"), "Arabic city and second page work");
        Check((await search.ReplyAsync("paid", "organization Fixture", default)).Contains("Current job"), "Organization filtering works");
        Check((await search.ReplyAsync("paid", "category محاسبة", default)).Contains("Current job"), "Category queries honor approved aliases");
        Check((await search.ReplyAsync("paid", "category Finance", default)).Contains("Current job"), "Approved main category names can be queried");
        settings.CityEnabled = false; await db.SaveChangesAsync();
        Check(!(await search.ReplyAsync("paid", "city Hebron", default)).Contains("Current job"), "Disabled criterion cannot be queried");
        settings.PaidOnly = false; settings.CityEnabled = true; await db.SaveChangesAsync();
        Check((await search.ReplyAsync("free", "city Hebron", default)).Contains("Current job"), "All audience allows activated linked free accounts");
        Check(!(await search.ReplyAsync("free", "city Hebron | -1", default)).Contains("Current job"), "Invalid pagination is rejected");
        Check(!(await search.ReplyAsync("free", "city Hebron | 101", default)).Contains("Current job"), "Pagination is bounded");
        var messaging = new TelegramMemberMessaging(db, config);
        Check(await messaging.Audience(true, null, now).CountAsync() == 1, "Paid announcement recipients exclude expired, future, cancelled, zero, suspended and opted-out accounts");
        Check(await messaging.Audience(false, null, now).CountAsync() == 6, "All announcement recipients still respect activation, links and opt-out");
        Check(messaging.Render("Hello {{name}} {{site}}", profiles[0]).Contains("Member paid https://fixture.invalid/ar"), "Optional variables render per recipient");
        Check(messaging.Render("Plain text", profiles[0]) == "Plain text", "Name can be omitted entirely");
        Check(messaging.Render("{{name}}", new MemberProfile { Name = "{{site}}" }) == "{{site}}", "Member names cannot introduce extra template variables");
        Check(!messaging.Fits("{{unknown}}") && !messaging.Fits("{{name") && !messaging.Fits(string.Concat(Enumerable.Repeat("{{name}}", 40))), "Unknown/malformed variables and oversized expanded messages are rejected");
        var idempotency = Guid.NewGuid();
        Check(await messaging.QueueAsync(idempotency, "Hello {{name}}", true, null, "Fixture", default), "Explicit request queues a broadcast without provider access");
        await messaging.QueueAsync(idempotency, "Hello {{name}}", true, null, "Fixture", default);
        Check(await db.TelegramMemberBroadcasts.CountAsync() == 1, "Repeated send confirmation does not duplicate the broadcast");
        await messaging.ExpandAsync(default); await messaging.ExpandAsync(default);
        var queued = await db.MemberMessages.SingleAsync();
        Check(queued.UserId == "paid" && queued.Text == "Hello Member paid" && queued.Kind == MemberMessageKind.Announcement, "Worker expansion personalizes and does not repeat messages");
        Check(await messaging.CanDeliverAsync(queued, default), "Eligible recipient can be delivered");
        profiles[0].NotificationsEnabled = false; await db.SaveChangesAsync();
        Check(!await messaging.CanDeliverAsync(queued, default), "Opting out after queueing cancels delivery");
        profiles[0].NotificationsEnabled = true;
        var active = await db.MemberSubscriptions.SingleAsync(x => x.UserId == "paid"); active.Cancelled = true; await db.SaveChangesAsync();
        Check(!await messaging.CanDeliverAsync(queued, default), "Subscription cancellation after queueing cancels paid-only delivery");
        Check(!await messaging.QueueAsync(Guid.NewGuid(), "Hello", true, "suspended", "Fixture", default), "Empty audiences cannot be queued");
        var preferences = new MemberPreferencePageService(new MemberPreferenceService(db));
        var input = await preferences.ReadAsync("free", true, default);
        input.Categories = ["Accounting"]; input.Locations = ["الخليل"];
        Check(await preferences.EditAsync("free", input, null, null, null, null, null, default), "Admin-targeted preference service saves validated choices");
        Check((await new MemberPreferenceService(db).ReadAsync("free", default)).Categories.SequenceEqual(new[] { "Accounting" }), "Only selected member preferences are changed");
        Check((await new MemberPreferenceService(db).ReadAsync("paid", default)).Categories.Length == 0, "Other member preferences are untouched");
        var protector = new EphemeralDataProtectionProvider();
        var controller = new TelegramMembersController(db, config, protector) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Check(await controller.Send("tampered", default) is BadRequestResult, "Send requires a protected preview payload");
        var preview = await controller.Preview(new() { Body = "Preview {{name}}", PaidOnly = false }, default);
        Check(preview is ViewResult && controller.ViewData["Sample"] is string sample && sample.StartsWith("Preview Member"), "Preview renders a real sample without sending");
        Check(await db.TelegramMemberBroadcasts.CountAsync() == 1, "Preview never queues or sends messages");
        Check(typeof(TelegramMembersController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles == "Admin", "Tools are admin-only");
        Check(typeof(TelegramMembersController).GetMethod("Send")!.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true), "Send requires antiforgery validation");
        config["Telegram:Enabled"] = "true";
        config["Telegram:BotToken"] = "123456789:fixture-token-for-offline-test";
        config["Telegram:BotUsername"] = "FixtureHiringBot";
        config["Telegram:WebhookSecret"] = new string('x', 32);
        using var handler = new RecordingTelegram();
        using var http = new HttpClient(handler);
        var client = new TelegramClient(http, config);
        var notifications = new MemberNotifications(db, client, config);
        await notifications.DeliverAsync(default);
        Check(queued.CancelledAt is not null && handler.Payloads.Count == 0, "Delivery worker cancels an ineligible queued announcement without contacting Telegram");
        var deliverId = Guid.NewGuid();
        await messaging.QueueAsync(deliverId, "Direct {{name}}", false, "free", "Fixture", default);
        await messaging.ExpandAsync(default);
        await notifications.DeliverAsync(default);
        Check(handler.Payloads.Count == 1 && handler.Payloads[0].Contains("Direct Member free")
            && (await db.MemberMessages.SingleAsync(x => x.EventKey == "announcement:" + deliverId)).SentAt is not null,
            "Delivery worker sends the personalized plain text through an isolated fake transport and records success");
        var commands = new TelegramCommands(db, client, null!, null!, config);
        await commands.HandleAsync("{\"message\":{\"from\":{\"id\":101},\"chat\":{\"id\":101,\"type\":\"private\"},\"text\":\"/jobs city Hebron\"}}", default);
        Check(handler.Payloads.Count == 2 && handler.Payloads.Last().Contains("Current job"), "Real /jobs command routes an activated linked member to bounded search");
        await commands.HandleAsync("{\"message\":{\"from\":{\"id\":101},\"chat\":{\"id\":999,\"type\":\"private\"},\"text\":\"/jobs city Hebron\"}}", default);
        Check(handler.Payloads.Count == 2, "Commands cannot search on behalf of a different Telegram chat");
        string Text() => System.Text.Json.JsonDocument.Parse(handler.Payloads.Last()).RootElement.GetProperty("text").GetString()!;
        (string Text, string Data)[] Buttons() => System.Text.Json.JsonDocument.Parse(handler.Payloads.Last()).RootElement
            .GetProperty("reply_markup").GetProperty("inline_keyboard").EnumerateArray().SelectMany(row => row.EnumerateArray())
            .Select(button => (button.GetProperty("text").GetString()!, button.GetProperty("callback_data").GetString()!)).ToArray();
        await commands.HandleAsync("{\"message\":{\"from\":{\"id\":101},\"chat\":{\"id\":101,\"type\":\"private\"},\"text\":\"بحث\"}}", default);
        Check(Buttons().Take(3).Select(x => x.Text).SequenceEqual(new[] { "حسب المجال", "حسب المكان", "حسب الجهة" }),
            "Arabic search command opens three criterion buttons");
        var categoryMenu = Buttons()[0].Data;
        var navigator = new TelegramJobNavigator(db, client, config);
        await navigator.OpenAsync(101, 101, categoryMenu, default);
        Check(Buttons().Any(x => x.Text == "Finance") && !Buttons().Any(x => x.Text == "محاسبة"),
            "Category menu shows one approved main group instead of duplicate aliases");
        var categoryPick = Buttons().Single(x => x.Text == "Finance").Data;
        var callbackJson = System.Text.Json.JsonSerializer.Serialize(new { callback_query = new { id = "fixture-callback", from = new { id = 101 },
            message = new { chat = new { id = 101, type = "private" } }, data = categoryPick } });
        await commands.HandleAsync(callbackJson, default);
        Check(handler.Methods[^2] == "answerCallbackQuery" && handler.Methods[^1] == "sendMessage" && Text().Contains("حسب المجال: Finance"),
            "Button press is acknowledged and chosen category appears in the filter summary");
        await navigator.OpenAsync(101, 101, Buttons().Single(x => x.Text == "عرض النتائج").Data, default);
        Check(Text().Contains("Current job 0"), "Explicit Show results applies the selected category");
        Check(Buttons().Any(x => x.Text == "وظائف أخرى") && Buttons().Any(x => x.Text == "تعديل الفلاتر"),
            "Results offer next-page and change-selection buttons");
        await navigator.OpenAsync(101, 101, Buttons().Single(x => x.Text == "وظائف أخرى").Data, default);
        Check(Text().Contains("Current job 6") && Buttons().Any(x => x.Text == "الوظائف السابقة"), "Results next/previous buttons preserve the filter");
        await navigator.OpenAsync(101, 101, "jobs:c:city:1", default);
        Check(Buttons().Any(x => x.Text == "الخليل"), "Location menu offers places with current jobs");
        await navigator.OpenAsync(101, 101, Buttons().Single(x => x.Text == "الخليل").Data, default);
        Check(Text().Contains("حسب المكان: الخليل"), "Selected location is retained before showing results");
        await navigator.OpenAsync(101, 101, Buttons().Single(x => x.Text == "عرض النتائج").Data, default);
        Check(Text().Contains("Current job"), "Selected location filters the results");
        db.Opportunities.Add(new Opportunity { Title = "Other organization branch", Organization = "Fixture Company Branch", Locations = "الخليل", Status = PublicationStatus.Published });
        await db.SaveChangesAsync();
        await navigator.OpenAsync(101, 101, "jobs:c:organization:1", default);
        var company = Buttons().Single(x => x.Text == "Fixture Company").Data;
        await navigator.OpenAsync(101, 101, company, default);
        await navigator.OpenAsync(101, 101, Buttons().Single(x => x.Text == "عرض النتائج").Data, default);
        Check(Text().Contains("Current job") && !Text().Contains("Other organization branch"), "Organization choice matches that organization exactly");
        await navigator.OpenAsync(101, 101, "jobs:p:organization:0000000000000000:1", default);
        Check(Text().Contains("راجع الفلاتر") && !Text().Contains("Current job"), "Stale or forged option keys require review before searching");
        settings.CityEnabled = false; await db.SaveChangesAsync();
        await navigator.OpenAsync(101, 101, "jobs:p:city:" + TelegramJobNavigator.Key("الخليل") + ":1", default);
        Check(!Buttons().Any(x => x.Text == "حسب المكان") && !Text().Contains("Current job"), "Previously issued buttons cannot bypass a newly disabled criterion");
        settings.CityEnabled = true; settings.PaidOnly = true; await db.SaveChangesAsync();
        await commands.HandleAsync(callbackJson, default);
        Check(Text().Contains("اشتراكًا") && !Text().Contains("Current job"), "Callbacks recheck newly changed audience permissions");
        settings.PaidOnly = false; await db.SaveChangesAsync();
        var beforeForged = handler.Payloads.Count;
        await commands.HandleAsync(callbackJson.Replace("\"id\":101,\"type\":\"private\"", "\"id\":999,\"type\":\"private\""), default);
        Check(handler.Payloads.Count == beforeForged, "Callback sender must own the private chat");
        for (var i = 0; i < 12; i++) db.Opportunities.Add(new Opportunity { Title = "Extra option job " + i, Organization = "Extra Organization " + i,
            Locations = "نابلس", Status = PublicationStatus.Published });
        await db.SaveChangesAsync();
        await navigator.OpenAsync(101, 101, "jobs:c:organization:1", default);
        Check(Buttons().Count(x => x.Data.StartsWith("jobs:f:p:")) == 8 && Buttons().Any(x => x.Text == "خيارات أخرى"),
            "Long option catalogs are paginated to eight choices");
        Check(Buttons().All(x => System.Text.Encoding.UTF8.GetByteCount(x.Data) <= 64), "Callback payloads stay within Telegram's 64-byte limit");
        await navigator.OpenAsync(101, 101, Buttons().Single(x => x.Text == "خيارات أخرى").Data, default);
        Check(Buttons().Any(x => x.Text == "الخيارات السابقة") && Buttons().Any(x => x.Text == "العودة للفلاتر"),
            "Option pages support previous and back-to-search navigation");
        db.Opportunities.AddRange(
            new Opportunity { Title = "Wrong city", Organization = "Fixture Company", Locations = "نابلس", Category = "Accounting", Status = PublicationStatus.Published, CreatedAt = now.AddMinutes(-20) },
            new Opportunity { Title = "Wrong organization", Organization = "Another Company", Locations = "الخليل", Category = "Accounting", Status = PublicationStatus.Published, CreatedAt = now.AddMinutes(-21) },
            new Opportunity { Title = "Wrong category", Organization = "Fixture Company", Locations = "الخليل", Category = "IT", Status = PublicationStatus.Published, CreatedAt = now.AddMinutes(-22) },
            new Opportunity { Title = "Approved alias match", Organization = "Fixture Company", Locations = "Hebron", Category = "محاسبة", Status = PublicationStatus.Published, CreatedAt = now.AddMinutes(-23) });
        await db.SaveChangesAsync();
        async Task Click(string label) => await navigator.OpenAsync(101, 101, Buttons().Single(button => button.Text == label).Data, default);
        await navigator.OpenAsync(101, 101, "jobs:m", default);
        Check(Text().Contains("حسب المكان: الكل") && Text().Contains("حسب الجهة: الكل") && Text().Contains("حسب المجال: الكل"),
            "New search defaults every independent filter to All");
        await Click("حسب المكان"); await Click("الخليل");
        await Click("حسب الجهة");
        Check(Buttons().Any(button => button.Text == "Another Company") && !Buttons().Any(button => button.Text.StartsWith("Extra Organization")),
            "Organization choices depend on the selected location");
        await Click("Fixture Company"); await Click("حسب المجال");
        Check(Buttons().Any(button => button.Text == "Finance") && Buttons().Any(button => button.Text == "IT")
            && Buttons().All(button => System.Text.Encoding.UTF8.GetByteCount(button.Data) <= 64),
            "Category options retain both prior selections within Telegram payload limits");
        await Click("Finance");
        var tripleResults = Buttons().Single(button => button.Text == "عرض النتائج").Data;
        Check(Text().Contains("حسب المكان: الخليل") && Text().Contains("حسب الجهة: Fixture Company") && Text().Contains("حسب المجال: Finance"),
            "All three selections remain visible together in the search summary");
        Check(Buttons().All(button => System.Text.Encoding.UTF8.GetByteCount(button.Data) <= 64),
            "Fully populated combined-filter callbacks fit within 64 bytes");
        await Click("عرض النتائج");
        Check(Text().Contains("Current job 0") && !Text().Contains("Wrong city") && !Text().Contains("Wrong organization") && !Text().Contains("Wrong category"),
            "Combined results require location AND exact organization AND approved category");
        await Click("وظائف أخرى");
        Check(Text().Contains("Current job 6") && Text().Contains("Approved alias match")
            && !Text().Contains("Wrong city") && !Text().Contains("Wrong organization") && !Text().Contains("Wrong category"),
            "All three filters and category aliases are preserved on subsequent result pages");
        await Click("الوظائف السابقة");
        Check(Text().Contains("Current job 0") && !Text().Contains("Approved alias match"), "Previous result page retains the same combined filters");
        await Click("تعديل الفلاتر"); await Click("حسب الجهة");
        Check(Buttons().Any(button => button.Text == "Another Company") && !Buttons().Any(button => button.Text == "Fixture Company Branch"),
            "Editing one filter narrows its choices using the other two");
        await Click("Another Company"); await Click("عرض النتائج");
        Check(Text().Contains("Wrong organization") && !Text().Contains("Current job"), "Changing one selection preserves the other two filters");
        await navigator.OpenAsync(101, 101, tripleResults, default);
        Check(Text().Contains("Current job 0") && !Text().Contains("Wrong organization"),
            "Earlier result buttons retain their own complete selection instead of a shared mutable session");
        await Click("تعديل الفلاتر"); await Click("حسب الجهة"); await Click("الكل");
        Check(Text().Contains("حسب الجهة: الكل") && Text().Contains("حسب المكان: الخليل") && Text().Contains("حسب المجال: Finance"),
            "All clears only its own filter while preserving the other choices");
        await Click("عرض النتائج"); await Click("وظائف أخرى");
        Check(Text().Contains("Wrong organization") && !Text().Contains("Wrong city") && !Text().Contains("Wrong category"),
            "Two selected filters still combine when organization is All");
        await Click("مسح الفلاتر");
        Check(Text().Contains("حسب المكان: الكل") && Text().Contains("حسب الجهة: الكل") && Text().Contains("حسب المجال: الكل"),
            "Clear filters resets every selection together");
        await Click("عرض النتائج");
        Check(Text().Contains("Extra option job"), "All filters cleared lists current published jobs");
        settings.CityEnabled = false; await db.SaveChangesAsync();
        await navigator.OpenAsync(101, 101, tripleResults, default);
        Check(Text().Contains("راجع الفلاتر") && !Buttons().Any(button => button.Text == "حسب المكان") && !Text().Contains("Current job"),
            "Disabling a selected filter forces a review instead of silently widening results");
        Check(!(await search.FiltersAsync("free", new(City: "الخليل"), 1, default)).Text.Contains("Current job"),
            "Combined query API also rejects a disabled selected criterion");
        settings.CityEnabled = true; settings.PaidOnly = true; await db.SaveChangesAsync();
        await navigator.OpenAsync(101, 101, tripleResults, default);
        Check(Text().Contains("اشتراكًا") && !Text().Contains("Current job"), "Combined results recheck paid-only access on every click");
        settings.PaidOnly = false; profiles[1].Suspended = true; await db.SaveChangesAsync();
        await navigator.OpenAsync(101, 101, tripleResults, default);
        Check(!Text().Contains("Current job") && Text().Contains("فعّله"), "Suspension revokes previously issued combined-search buttons");
        profiles[1].Suspended = false; await db.SaveChangesAsync();
        var matching = await db.Opportunities.Where(item => item.Status == PublicationStatus.Published && item.Organization == "Fixture Company"
            && (item.Category == "Accounting" || item.Category == "محاسبة") && (item.Locations == "الخليل" || item.Locations == "Hebron")).ToListAsync();
        foreach (var item in matching) item.Status = PublicationStatus.Archived;
        await db.SaveChangesAsync();
        await navigator.OpenAsync(101, 101, tripleResults, default);
        Check(Text().Contains("لا توجد وظائف سارية مطابقة") && Buttons().Any(button => button.Text == "تعديل الفلاتر"),
            "A valid combination that becomes empty offers filter editing rather than unrelated jobs");
        foreach (var item in await db.Opportunities.Where(item => item.Category == "Accounting" || item.Category == "محاسبة").ToListAsync()) item.Category = "IT";
        await db.SaveChangesAsync();
        await navigator.OpenAsync(101, 101, tripleResults, default);
        Check(Text().Contains("راجع الفلاتر") && Text().Contains("حسب المجال: الكل") && Text().Contains("حسب المكان: الخليل"),
            "Disappearing options retain other choices and require review before broadening the search");
        await navigator.OpenAsync(101, 101, "jobs:f:r:forged:1", default);
        Check(Text().Contains("راجع الفلاتر") && !Text().Contains("Current job"), "Malformed filter snapshots never trigger a results query");
        Check(!(await search.FiltersAsync("free", TelegramJobFilters.Empty, 101, default)).Text.Contains("Extra option job"),
            "Combined pagination remains bounded");
        await commands.HandleAsync("{\"message\":{\"from\":{\"id\":101},\"chat\":{\"id\":101,\"type\":\"private\"},\"text\":\"/help\"}}", default);
        using (var help = System.Text.Json.JsonDocument.Parse(handler.Payloads.Last()))
        {
            var text = help.RootElement.GetProperty("text").GetString()!;
            var buttons = help.RootElement.GetProperty("reply_markup").GetProperty("inline_keyboard")
                .EnumerateArray().SelectMany(row => row.EnumerateArray()).ToArray();
            Check(text.StartsWith("📋 دليل") && text.Contains("\n\u2066/search\u2069\n") && !text.Contains("https://"),
                "Arabic help isolates current commands on separate lines and keeps raw URLs out of the guide");
            Check(buttons.Count(x => x.TryGetProperty("callback_data", out _)) == 1
                && buttons.Count(x => x.TryGetProperty("url", out _)) == 4,
                "Help preserves both its search callback and all account navigation links in one keyboard");
            Check(buttons.Single(x => x.TryGetProperty("callback_data", out _)).GetProperty("callback_data").GetString() == "jobs:m"
                && buttons.Any(x => x.TryGetProperty("url", out var link) && link.GetString()!.EndsWith("/Member/Settings")),
                "Help search button opens the real menu and alert resumption points to account settings");
        }
        profiles[1].Language = "en"; await db.SaveChangesAsync();
        await commands.HandleAsync("{\"message\":{\"from\":{\"id\":101},\"chat\":{\"id\":101,\"type\":\"private\"},\"text\":\"/help\"}}", default);
        Check(Text().StartsWith("📋 Bot help") && Text().Contains("/search") && Text().Contains("ORDER_ID"),
            "Help follows member language and explains the payment command argument");
        await navigator.OpenAsync(101, 101, "jobs:m", default);
        await Click("By location"); await Click("Hebron");
        await Click("By organization"); await Click("Fixture Company");
        await Click("By category"); await Click("IT");
        Check(Text().Contains("By location: Hebron") && Text().Contains("By organization: Fixture Company") && Text().Contains("By category: IT"),
            "English menus preserve the same three canonical filters with localized location names");
        await Click("Show results");
        Check(Text().Contains("Wrong category") && Buttons().Any(button => button.Text == "Edit filters"),
            "English combined search applies all filters and offers localized navigation");
        profiles[1].Language = "ar"; await db.SaveChangesAsync();
        var localConfig = new ConfigurationBuilder().AddInMemoryCollection(config.AsEnumerable()).AddInMemoryCollection(new Dictionary<string, string?>
        { ["Runtime:Mode"] = "Local", ["Telegram:LocalTestingEnabled"] = "true" }).Build();
        var localServices = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        localServices.AddSingleton<IConfiguration>(localConfig);
        localServices.AddScoped<HiringDb>(_ => new FixtureDb(new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection).Options));
        localServices.AddSingleton(client);
        localServices.AddScoped(sp => new TelegramCommands(sp.GetRequiredService<HiringDb>(), client, null!, null!, localConfig));
        localServices.AddScoped(sp => new MemberNotifications(sp.GetRequiredService<HiringDb>(), client, localConfig));
        using (var localProvider = localServices.BuildServiceProvider())
        {
            db.MembershipRuntimeStates.Add(new() { Id = 1 });
            db.TelegramInbox.Add(new() { Id = 700001, Json = "{\"message\":{\"from\":{\"id\":101},\"chat\":{\"id\":101,\"type\":\"private\"},\"text\":\"/search\"}}" });
            db.MemberMessages.Add(new() { UserId = "free", Kind = MemberMessageKind.Opportunity, EventKey = "local-no-automatic-alert", Text = "Automatic alert" });
            await db.SaveChangesAsync();
            var localBroadcast = Guid.NewGuid();
            await messaging.QueueAsync(localBroadcast, "Local announcement", false, "free", "Fixture", default);
            var pendingPublications = await db.OpportunityPublications.CountAsync(x => !x.Completed);
            var worker = new MembershipWorker(localProvider.GetRequiredService<IServiceScopeFactory>(), Microsoft.Extensions.Logging.Abstractions.NullLogger<MembershipWorker>.Instance);
            var tick = typeof(MembershipWorker).GetMethod("TickAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            await (Task)tick.Invoke(worker, new object[] { CancellationToken.None })!;
            Check((await db.TelegramInbox.AsNoTracking().SingleAsync(x => x.Id == 700001)).ProcessedAt is not null,
                "Opted-in local worker actually processes bot updates without payment recovery services or tables");
            Check((await db.MemberMessages.AsNoTracking().SingleAsync(x => x.EventKey == "announcement:" + localBroadcast)).SentAt is not null,
                "Local worker delivers an explicitly queued admin announcement through the fake Telegram transport");
            var automatic = await db.MemberMessages.AsNoTracking().SingleAsync(x => x.EventKey == "local-no-automatic-alert");
            Check(automatic.Attempts == 0 && automatic.SentAt is null && automatic.CancelledAt is null
                && await db.OpportunityPublications.CountAsync(x => !x.Completed) == pendingPublications,
                "Local testing neither claims automatic alerts nor expands publication notifications");
            localConfig["Telegram:LocalTestingEnabled"] = "false";
            db.TelegramInbox.Add(new() { Id = 700002, Json = "{}" }); await db.SaveChangesAsync();
            await (Task)tick.Invoke(worker, new object[] { CancellationToken.None })!;
            Check((await db.TelegramInbox.AsNoTracking().SingleAsync(x => x.Id == 700002)).Attempts == 0,
                "Local bot processing stays off without the explicit local testing opt-in");
        }
        for (var i = 0; i < 205; i++)
            db.MemberProfiles.Add(new() { UserId = "batch-" + i.ToString("D3"), Name = "Batch", Activated = true,
                TelegramChatId = i + 1000, TelegramUserId = i + 1000, CreatedAt = now.AddDays(-1) });
        await db.SaveChangesAsync();
        var batchId = Guid.NewGuid();
        await messaging.QueueAsync(batchId, "Batch", false, null, "Fixture", default);
        await messaging.ExpandAsync(default);
        Check(await db.MemberMessages.CountAsync(x => x.EventKey == "announcement:" + batchId) == 100,
            "Large audience expansion is bounded to 100 recipients per tick");
        await messaging.ExpandAsync(default); await messaging.ExpandAsync(default);
        var countAfter = await db.MemberMessages.CountAsync(x => x.EventKey == "announcement:" + batchId);
        await messaging.ExpandAsync(default);
        Check(countAfter == 211 && await db.MemberMessages.CountAsync(x => x.EventKey == "announcement:" + batchId) == countAfter,
            "Expansion resumes across batches without skipped or duplicated recipients");
        using var shape = new HiringDb(new DbContextOptionsBuilder<HiringDb>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Shape;Trusted_Connection=True").Options);
        Check(!shape.Database.HasPendingModelChanges(), "SQL Server migration snapshot matches the model");
        Console.WriteLine($"Telegram member tools: {passed} checks passed; no external messages sent.");
    }
    private sealed class RecordingTelegram : HttpMessageHandler
    {
        public List<string> Payloads { get; } = [];
        public List<string> Methods { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Methods.Add(request.RequestUri!.Segments.Last());
            Payloads.Add(await request.Content!.ReadAsStringAsync(ct));
            return new(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") };
        }
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
