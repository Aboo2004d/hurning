using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

public static class OperationChecks
{
    private static void AcceptFixtureChanges(HiringDb db)
    {
        // Assign synthetic database-generated identities before accepting the fixture snapshot.
        var id = 1000;
        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            foreach (var property in entry.Properties.Where(x => x.IsTemporary).ToList())
            {
                if (property.Metadata.ClrType == typeof(long))
                    property.CurrentValue = (long)++id;
                else if (property.Metadata.ClrType == typeof(int))
                    property.CurrentValue = ++id;
                property.IsTemporary = false;
            }
            entry.State = EntityState.Unchanged;
        }
    }

    public static Task RunAsync()
    {
        var passed = 0;
        void Check(bool value, string text)
        {
            if (!value)
                throw new Exception("FAIL: " + text);
            passed++;
            Console.WriteLine("PASS: " + text);
        }
        using var db = new HiringDb(new DbContextOptionsBuilder<HiringDb>()
            .UseSqlServer("Server=127.0.0.1;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=True").Options);
        var history = new OperationReadService(db);
        var sql = history.Query(new OperationFilterDto("source", OperationArea.Collection, OperationOutcome.Failed,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "Worker")).Skip(20).Take(20).ToQueryString();
        Check(sql.Contains("OFFSET") && sql.Contains("ORDER BY") && sql.Contains("[Audit]"), "history filters, ordering and pagination translate to SQL without opening a connection");
        var upgrade = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().GenerateScript(
            "20261020052258_PlatformImprovements", "20261020182000_OperationHistoryIndex");
        Check(upgrade.Contains("CREATE INDEX [IX_Audit_At_Id]") && !upgrade.Contains("DROP TABLE")
            && !db.Database.HasPendingModelChanges(), "operation history migration only adds its index and matches the EF model");
        var tracker = new OperationTracking();
        var opportunity = new Opportunity { Title = "Public job", Summary = "PRIVATE SOURCE PAYLOAD", Status = PublicationStatus.NeedsReview };
        var source = new Source { Name = "Public source", ConfigurationJson = "SECRET CONFIG" };
        var run = new ScrapeRun { SourceId = 11 };
        db.AddRange(opportunity, source, run);
        tracker.Stage(db);
        Check(db.Audit.Local.Count == 3, "new opportunity, source and collection start each produce an event");
        Check(db.Audit.Local.All(x => !x.Detail.Contains("PRIVATE") && !x.Detail.Contains("SECRET")), "events omit source content and configuration");
        tracker.Stage(db);
        Check(db.Audit.Local.Count == 3, "staging again before save replaces pending events without duplicates");
        // Emulate successful persistence; subsequent saves record transitions, not the old additions.
        AcceptFixtureChanges(db);
        tracker.Stage(db);
        Check(db.Audit.Local.Count == 3, "unchanged entities do not create repeated events");
        run.FinishedAt = DateTimeOffset.UtcNow;
        run.Status = "Failed";
        run.Found = 12;
        run.Changed = 3;
        tracker.Stage(db);
        var completed = db.Audit.Local.Where(x => x.Action == "Activity.Collection.Failed.Run").ToList();
        Check(db.Audit.Local.Count == 4 && completed.Count == 1 && completed[0].Detail.Contains("Found 12"),
            "collection completion stores failure and counters as a separate durable event");
        db.ChangeTracker.Clear();
        var message = new MemberMessage { Id = 42, Text = "PRIVATE MESSAGE", UserId = "PRIVATE MEMBER", Kind = MemberMessageKind.Opportunity };
        db.Attach(message);
        message.AvailableAt = message.AvailableAt.AddMinutes(5);
        message.LastError = "TelegramBlocked";
        message.Attempts = 1;
        tracker.Stage(db);
        Check(db.Audit.Local.Single().Action == "Activity.Telegram.Failed.BotDelivery"
            && !db.Audit.Local.Single().Detail.Contains("PRIVATE"), "delivery failure produces safe history without chat IDs or message content");
        AcceptFixtureChanges(db);
        message.AvailableAt = message.AvailableAt.AddMinutes(10);
        message.Attempts++;
        tracker.Stage(db);
        Check(db.Audit.Local.Count == 2, "repeated delivery failure remains a separate attempt even with the same error code");
        db.ChangeTracker.Clear();
        var publication = new SocialPublication { Id = Guid.NewGuid(), Status = "Pending", OpportunityId = 99, Text = "PRIVATE POST" };
        db.Attach(publication);
        publication.Status = "Published";
        tracker.Stage(db);
        Check(db.Audit.Local.Single().Action == "Activity.Publishing.Succeeded.Publish", "confirmed provider publication is classified as successful");
        db.ChangeTracker.Clear();
        publication = new SocialPublication { Id = Guid.NewGuid(), Status = "Pending", OpportunityId = 99 };
        db.Attach(publication);
        publication.Status = "Unknown";
        tracker.Stage(db);
        Check(db.Audit.Local.Single().Action == "Activity.Publishing.Uncertain.Publish", "uncertain publication is never represented as a confirmed failure or success");
        for (var year = 2025; year <= 2027; year++)
        {
            for (var day = new DateOnly(year, 1, 1); day.Year == year; day = day.AddDays(1))
            {
                var start = OperationReadService.PalestineDayStart(day);
                if (SourceSchedule.LocalDate(start) != day)
                    throw new Exception("Invalid Palestine day boundary");
            }
        }
        Check(true, "Palestine date filter boundaries cover DST transitions across three years");
        var item = new Opportunity { Title = "مهندس & <اختبار>", Summary = new string('a', 12000), OriginalLanguage = "ar", Kind = OpportunityKind.Job };
        var link = "https://hiring.ps/ar/Opportunities/Details/99";
        Check(TelegramBotTemplates.Compose(TelegramBotTemplates.DefaultBody, item, link) == item.Title + "\n" + link,
            "unconfigured bot template preserves the previous title and mandatory link exactly");
        var bot = TelegramBotTemplates.Compose("{{title}}\n{{summary}}", item, link, "Tomorrow: ");
        Check(bot.Length <= 4000 && bot.EndsWith(link) && bot.StartsWith("Tomorrow:"), "long bot templates retain the reminder and URL inside the stored message limit");
        Check(SocialTemplateManagementService.ValidScope("TelegramBot", "Job")
            && SocialTemplateManagementService.ValidScope("Telegram", "general")
            && SocialPublishing.TemplateProvider("TelegramBot") != SocialPublishing.TemplateProvider("Telegram"),
            "channel and bot templates are independent providers with the same general/type fallback pattern");
        var now = DateTimeOffset.UtcNow;
        var a = new ToolWorkerState { Id = "a", At = now.AddSeconds(-1) };
        var b = new ToolWorkerState { Id = "b", At = now };
        var workers = new Dictionary<string, ToolWorkerState> { [a.Id] = a, [b.Id] = b };
        var results = ToolMonitoringRules.StandardTools.Select(tool => new ToolCheckResult { Tool = tool, WorkerId = "a", CheckedAt = now, State = ToolState.Ready }).ToList();
        results.Single(x => x.Tool == "Chromium").WorkerId = "b";
        Check(results.All(x => ToolMonitoringRules.ResultFresh(x, workers.GetValueOrDefault(x.WorkerId), now)),
            "checking one tool on another live worker keeps all other tool results verified");
        b.At = now.AddMinutes(-5);
        Check(results.Where(x => x.Tool != "Chromium").All(x => ToolMonitoringRules.ResultFresh(x, workers.GetValueOrDefault(x.WorkerId), now))
            && !ToolMonitoringRules.ResultFresh(results.Single(x => x.Tool == "Chromium"), b, now),
            "stopping one producer only makes its own results stale");
        Console.WriteLine($"Operation and Telegram checks passed: {passed}");
        return Task.CompletedTask;
    }
}
