using HiringPs.Web.Areas.Admin.Controllers;
using HiringPs.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using HiringPs.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>Exercises daily scheduling, eligibility and durable no-repeat reservations without real publishing.</summary>
internal static class RandomPublishingChecks
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
        Check(RandomPublicationRules.Valid(2, 7, ["09:00", "09:05"]), "default two posts are five minutes apart");
        Check(!RandomPublicationRules.Valid(2, 7, ["09:00", "09:04"]), "less than five-minute spacing rejected");
        Check(!RandomPublicationRules.Valid(2, 7, ["10:00", "09:00"]), "unordered times rejected");
        Check(!RandomPublicationRules.Valid(3, 7, ["09:00", "09:05"]), "count must match schedule length");
        Check(!RandomPublicationRules.Valid(1, -1, ["09:00"]) && !RandomPublicationRules.Valid(25, 7, []), "bounds enforced");
        Check(!RandomPublicationRules.Valid(1, 7, ["25:00"]), "invalid clock rejected");
        Check(!RandomPublicationRules.Valid(2, 7, ["00:00", "23:59"]), "daily midnight spacing protected");
        Check(RandomPublicationRules.Times("invalid").Length == 0, "corrupt schedule does not trigger publishing");
        var day = new DateOnly(2026, 11, 2);
        var instant = RandomPublicationRules.Instant(day, "09:00")!.Value;
        Check(TimeZoneInfo.ConvertTime(instant, TextRules.PalestineTimeZone).Hour == 9, "schedule converts Palestine wall time to UTC");

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation("Latin1_General_100_BIN2", string.CompareOrdinal);
        await using var db = new FixtureDb(new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection).AddInterceptors(new FixtureLock()).Options);
        var tables = new[] { typeof(Opportunity), typeof(SocialPublication), typeof(SocialConnection), typeof(SocialTemplateVersion),
            typeof(RandomPublicationSetting), typeof(RandomPublicationRun), typeof(AuditEntry), typeof(OpportunityPublication), typeof(IndexNowEntry) }
            .Select(t => db.Model.FindEntityType(t)!.GetTableName()).ToArray();
        foreach (var sql in db.Database.GenerateCreateScript().Split(';', StringSplitOptions.RemoveEmptyEntries))
            if (tables.Any(t => sql.TrimStart().StartsWith($"CREATE TABLE \"{t}\""))
                || sql.TrimStart().StartsWith("CREATE UNIQUE INDEX \"IX_RandomPublicationRuns_"))
                await db.Database.ExecuteSqlRawAsync(sql);
        var service = new RandomPublicationSettingsService(db);
        Check((await service.ReadAsync(default)).All(x => !x.Enabled && x.DailyCount == 2), "all destinations default disabled with two posts");
        var clock = new Clock { Now = instant };
        var sender = new Sender();
        var scheduler = new RandomPublicationScheduler(db, sender, clock);
        await scheduler.TickAsync(default);
        Check(sender.Sent.Count == 0, "empty settings cause no external work");
        Check(await service.SaveAsync(new("Facebook", true, 2, 7, ["09:00", "09:05"], "", false, ""), "fixture", default)
            == "IntegrationNotConfigured", "cannot enable missing connection");
        db.SocialConnections.Add(new() { Provider = "Facebook", PageId = "123", PageName = "Page", Enabled = true });
        db.SocialConnections.Add(new() { Provider = "LinkedInPersonal", PageId = "456", PageName = "Profile", Enabled = true });
        await db.SaveChangesAsync();
        Check(await service.SaveAsync(new("Facebook", true, 2, 7, ["09:00", "09:05"], "", false, ""), "fixture", default) is null,
            "Facebook schedule saved independently");
        Check(await service.SaveAsync(new("LinkedInPersonal", false, 1, 3, ["12:00"], "", false, ""), "fixture", default) is null,
            "personal schedule has independent count, time and day limit");
        Check(await service.SaveAsync(new("Facebook", false, 2, 7, ["09:00", "09:05"], "outdated", false, ""), "fixture", default)
            == "RandomSettingsConflict", "stale settings cannot overwrite current configuration");
        for (var id = 1; id <= 12; id++) db.Opportunities.Add(new Opportunity
        {
            Id = id, Title = "Job " + id, Organization = "Employer", Summary = "Summary", Locations = "Gaza",
            Status = PublicationStatus.Draft, Kind = OpportunityKind.Job, Deadline = day.AddDays(2),
        });
        await db.SaveChangesAsync();
        // Bulk fixture updates avoid first-publication side effects; scheduler still reads real EF queries.
        await db.Opportunities.ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, PublicationStatus.Published));
        await db.Opportunities.Where(x => x.Id == 2).ExecuteUpdateAsync(s => s.SetProperty(x => x.Deadline, (DateOnly?)null));
        await db.Opportunities.Where(x => x.Id == 3).ExecuteUpdateAsync(s => s.SetProperty(x => x.Deadline, day.AddDays(-1)));
        await db.Opportunities.Where(x => x.Id == 4).ExecuteUpdateAsync(s => s.SetProperty(x => x.Deadline, day.AddDays(8)));
        await db.Opportunities.Where(x => x.Id == 5).ExecuteUpdateAsync(s => s.SetProperty(x => x.Kind, OpportunityKind.Training));
        await db.Opportunities.Where(x => x.Id == 6).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, PublicationStatus.Hidden));
        await db.Opportunities.Where(x => x.Id == 7).ExecuteUpdateAsync(s => s.SetProperty(x => x.ClosureEvidence, "closed"));
        db.SocialPublications.Add(new() { Id = Guid.NewGuid(), OpportunityId = 8, Provider = "Facebook", PageId = "123", Status = "Published" });
        db.SocialPublications.Add(new() { Id = Guid.NewGuid(), OpportunityId = 9, Provider = "Facebook", PageId = "123", Status = "Unknown" });
        db.SocialPublications.Add(new() { Id = Guid.NewGuid(), OpportunityId = 10, Provider = "Facebook", PageId = "123", Status = "Pending" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var candidates = await RandomPublicationRules.Candidates(db, "Facebook", day, 7).Select(x => x.Id).ToListAsync();
        Check(candidates.Order().SequenceEqual(new long[] { 1, 2, 11, 12 }), "filter preserves no-deadline jobs and excludes closed, old, far-away, non-jobs and prior attempts");
        Check(await RandomPublicationRules.Candidates(db, "LinkedInPersonal", day, 7).AnyAsync(x => x.Id == 8),
            "Facebook publication does not exclude the personal destination");
        await scheduler.TickAsync(default);
        Check(sender.Sent.Count == 1, "first due slot sends exactly one selected job");
        var first = sender.Sent.Single().OpportunityId;
        await scheduler.TickAsync(default);
        Check(sender.Sent.Count == 1, "same slot cannot be replayed");
        clock.Now = instant.AddMinutes(4);
        await scheduler.TickAsync(default);
        Check(sender.Sent.Count == 1, "second slot cannot run before its own time");
        clock.Now = instant.AddMinutes(5);
        await scheduler.TickAsync(default);
        Check(sender.Sent.Count == 2 && sender.Sent.Last().OpportunityId != first, "second slot selects a different job five minutes later");
        db.ChangeTracker.Clear();
        var facebook = await db.RandomPublicationSettings.SingleAsync(x => x.Provider == "Facebook");
        facebook.DailyCount = 3; facebook.TimesJson = "[\"09:00\",\"09:05\",\"09:10\"]";
        await db.SaveChangesAsync();
        sender.Throw = true;
        clock.Now = instant.AddMinutes(10);
        await scheduler.TickAsync(default);
        Check((await db.RandomPublicationRuns.SingleAsync(x => x.Slot == 3)).State == RandomPublicationState.Unknown,
            "uncertain send recorded without automatic resend");
        var sends = sender.Sent.Count;
        await scheduler.TickAsync(default);
        Check(sender.Sent.Count == sends, "uncertain slot stays consumed on next tick");
        db.ChangeTracker.Clear();
        facebook = await db.RandomPublicationSettings.SingleAsync(x => x.Provider == "Facebook");
        facebook.DailyCount = 4; facebook.TimesJson = "[\"09:00\",\"09:05\",\"09:10\",\"09:15\"]";
        await db.SaveChangesAsync();
        clock.Now = instant.AddMinutes(25);
        await scheduler.TickAsync(default);
        Check(sender.Sent.Count == sends && (await db.RandomPublicationRuns.SingleAsync(x => x.Slot == 4)).ResultCode == "MissedTime",
            "missed slot does not produce a catch-up burst");
        facebook.Enabled = false;
        await db.SaveChangesAsync();
        clock.Now = instant.AddDays(1);
        await scheduler.TickAsync(default);
        Check(sender.Sent.Count == sends, "disabled destination cannot publish on next day");
        // A fresh context simulates a restart; durable runs still exclude all previously chosen jobs.
        await using var restarted = new FixtureDb(new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection).AddInterceptors(new FixtureLock()).Options);
        var reused = sender.Sent.Where(x => x.OpportunityId != null).Select(x => x.OpportunityId!.Value).ToArray();
        Check(!await RandomPublicationRules.Candidates(restarted, "Facebook", day.AddDays(1), 7).AnyAsync(x => reused.Contains(x.Id)),
            "selected jobs remain excluded across restart and day boundary");
        var original = await restarted.RandomPublicationRuns.FirstAsync(x => x.OpportunityId != null);
        restarted.RandomPublicationRuns.Add(new() { Id = Guid.NewGuid(), Provider = original.Provider, Day = day.AddDays(5),
            Slot = 1, OpportunityId = original.OpportunityId, ScheduledAt = clock.Now, CreatedAt = clock.Now });
        try { await restarted.SaveChangesAsync(); throw new Exception("Unique job constraint missing"); }
        catch (DbUpdateException) { Check(true, "database unique index prevents duplicate job reservation"); restarted.ChangeTracker.Clear(); }
        restarted.RandomPublicationRuns.Add(new() { Id = Guid.NewGuid(), Provider = original.Provider, Day = original.Day,
            Slot = original.Slot, ScheduledAt = clock.Now, CreatedAt = clock.Now });
        try { await restarted.SaveChangesAsync(); throw new Exception("Unique slot constraint missing"); }
        catch (DbUpdateException) { Check(true, "database unique index prevents duplicate daily slot"); }
        restarted.ChangeTracker.Clear();
        var staleRun = new RandomPublicationRun { Provider = "LinkedInPersonal", Day = day, Slot = 1,
            OpportunityId = 12, PageId = "456", ScheduledAt = instant, CreatedAt = instant };
        restarted.RandomPublicationRuns.Add(staleRun);
        await restarted.SaveChangesAsync();
        await new RandomPublicationScheduler(restarted, sender, clock).TickAsync(default);
        Check(staleRun.State == RandomPublicationState.Unknown && sender.Sent.Count == sends,
            "interrupted reservation is classified uncertain without another send");
        var guardRun = new RandomPublicationRun { Provider = "LinkedInPersonal", Day = TextRules.PalestineToday(), Slot = 2,
            OpportunityId = 11, PageId = "456", ScheduledAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow };
        restarted.RandomPublicationRuns.Add(guardRun);
        await restarted.SaveChangesAsync();
        await restarted.Opportunities.Where(x => x.Id == 11).ExecuteUpdateAsync(update => update.SetProperty(x => x.Deadline, (DateOnly?)null));
        restarted.ChangeTracker.Clear();
        var publisher = new SocialPublicationService(restarted, null!, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        var guarded = await publisher.PublishAsync(new(guardRun.Id, 11, "LinkedInPersonal", null, "text", false, "fixture", new("456")), default);
        Check(guarded.Route == SocialPublicationRoute.InvalidRequest,
            "shared publication path rechecks disabled schedule before any network call");
        var personal = await restarted.RandomPublicationSettings.SingleAsync(x => x.Provider == "LinkedInPersonal");
        personal.Enabled = true;
        await restarted.SaveChangesAsync();
        guarded = await publisher.PublishAsync(new(guardRun.Id, 11, "LinkedInPersonal", null, "text", false, "fixture", new("wrong")), default);
        Check(guarded.Route == SocialPublicationRoute.InvalidRequest,
            "shared publication path prevents destination changes after reservation");
        var web = WebApplication.CreateBuilder();
        web.Logging.ClearProviders();
        web.WebHost.UseUrls("http://127.0.0.1:0");
        web.Services.AddControllers().AddApplicationPart(typeof(RandomPublishingController).Assembly);
        await using (var app = web.Build())
        {
            app.UseRouting();
            app.Use(async (context, next) =>
            {
                if (context.GetEndpoint() is { } endpoint)
                {
                    await context.Response.WriteAsJsonAsync(new { action = context.Request.RouteValues["action"],
                        controller = context.Request.RouteValues["controller"],
                        authorized = endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any(x => x.Roles == "Admin") });
                    return;
                }
                await next(context);
            });
            app.MapSiteRoutes();
            await app.StartAsync();
            var links = app.Services.GetRequiredService<LinkGenerator>();
            var path = links.GetPathByAction("Index", "RandomPublishing", new { area = "Admin" });
            Check(path == "/Admin/RandomPublishing", "admin navigation generates stable random publishing page route");
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            var routeText = await client.GetStringAsync(path);
            Check(routeText.Contains("\"authorized\":true") && routeText.Contains("\"controller\":\"RandomPublishing\""),
                "random settings page requires Admin role");
            var save = links.GetPathByAction("Save", "RandomPublishing", new { area = "Admin" });
            using var response = await client.PostAsync(save, new StringContent(""));
            routeText = await response.Content.ReadAsStringAsync();
            Check(response.IsSuccessStatusCode && routeText.Contains("\"action\":\"Save\"") && routeText.Contains("\"authorized\":true"),
                "settings POST resolves correct authorized action");
            await app.StopAsync();
        }
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        services.AddDbContext<HiringDb>(options => options.UseSqlServer("Server=127.0.0.1,1;Database=Unused;User Id=unused;Password=unused;TrustServerCertificate=True"));
        services.AddSocialPublishing();
        await using (var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }))
        await using (var scope = provider.CreateAsyncScope())
        {
            Check(scope.ServiceProvider.GetRequiredService<RandomPublicationScheduler>() is not null
                && scope.ServiceProvider.GetRequiredService<IRandomPublicationDispatcher>() is not null,
                "scheduled publishing dependencies resolve without opening a database or sending a request");
        }
        using var model = new DesignTimeFactory().CreateDbContext([]);
        Check(!model.Database.HasPendingModelChanges(), "generated migration matches current SQL Server model");
        var candidateSql = RandomPublicationRules.Candidates(model, "Facebook", day, 7).ToQueryString();
        Check(candidateSql.Contains("NOT EXISTS") && candidateSql.Contains("RandomPublicationRuns"), "candidate query translates to SQL Server without client filtering");
        Console.WriteLine($"Random publishing: {count} checks passed without external calls.");
    }
    // SQL Server application-lock statements are suppressed in this SQLite-only fixture.
    // Actual reservation uniqueness is still exercised through real database indexes.
    private sealed class FixtureLock : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default) =>
            ValueTask.FromResult(command.CommandText.StartsWith("DECLARE @lockResult")
                ? InterceptionResult<int>.SuppressWithResult(0) : result);
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Sender : IRandomPublicationDispatcher
    {
        public List<RandomPublicationRun> Sent { get; } = [];
        public bool Throw { get; set; }
        public Task<RandomPublicationDeliveryDto> SendAsync(RandomPublicationRun run, CancellationToken ct)
        {
            Sent.Add(run);
            if (Throw) throw new HttpRequestException("fixture uncertain send");
            return Task.FromResult(new RandomPublicationDeliveryDto(RandomPublicationState.Published, "Published"));
        }
    }
    private sealed class FixtureDb(DbContextOptions<HiringDb> options) : HiringDb(options)
    {
        protected override void OnModelCreating(ModelBuilder model)
        {
            base.OnModelCreating(model);
            foreach (var entity in model.Model.GetEntityTypes())
                foreach (var p in entity.GetProperties())
                {
                    if (p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)) p.SetValueConverter(new DateTimeOffsetToBinaryConverter());
                    if (p.Name == "Version" && p.ClrType == typeof(byte[])) p.ValueGenerated = ValueGenerated.Never;
                }
        }
    }
}
