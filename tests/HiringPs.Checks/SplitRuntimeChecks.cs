using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

internal static class SplitRuntimeChecks
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); count++; }
        IConfiguration Config(string role) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Runtime:Mode"] = role }).Build();
        Check(ApplicationRuntime.Mode(new ConfigurationBuilder().Build()) == ApplicationRuntimeMode.Server, "unconfigured web defaults to server services without collection");
        Check(!BackgroundRegistration.RunInWeb(Config("Local")), "local web never starts server delivery workers");
        var services = new ServiceCollection().AddServerWorkers();
        var hosted = services.Where(x => x.ServiceType == typeof(IHostedService)).Select(x => x.ImplementationType).ToArray();
        Check(hosted.Length == 2 && hosted.Contains(typeof(MembershipWorker)) && hosted.Contains(typeof(DistributionWorker)), "server starts membership/payment and publication workers only");
        Check(CollectorArguments.Parse([]).Help, "worker without arguments prints help rather than running forever");
        var command = CollectorArguments.Parse(["--collect", "--source", "17", "--checks"]);
        Check(command.Collect && command.SourceId == 17 && command.Checks, "explicit source collection can include queued tool checks");
        Check(CollectorArguments.Parse(["--once"]).Collect, "legacy once flag selects a complete manual round");
        foreach (var bad in new[] { new[] { "--source", "1" }, new[] { "--collect", "--source", "-1" }, new[] { "--collect", "--probe" }, new[] { "--source" } })
        {
            try { CollectorArguments.Parse(bad); throw new Exception("Expected invalid CLI rejection"); }
            catch (ArgumentException) { Check(true, "invalid or ambiguous command is rejected before database access"); }
        }
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var cache = new PublicCatalogCache();
        var options = new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection)
            .AddInterceptors(new PublicCatalogTransactionInterceptor()).Options;
        await using var db = new HiringDb(options, publicCatalog: cache, revisionWriter: new PublicCatalogRevisionWriter());
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE PublicCatalogRevisions (Id INTEGER PRIMARY KEY, Version INTEGER NOT NULL);
            INSERT INTO PublicCatalogRevisions VALUES(1,0);
            CREATE TABLE Audit (Id INTEGER PRIMARY KEY AUTOINCREMENT, Actor TEXT NOT NULL, Action TEXT NOT NULL, Detail TEXT NOT NULL, At TEXT NOT NULL);
            """);
        db.Audit.Add(new() { Actor = "fixture", Action = "Location.Approved", Detail = "fixture location" });
        var before = cache.Snapshot();
        await db.SaveChangesAsync();
        Check(await Version() == 1 && before.Changed.HasChanged, "actual database save commits revision and local invalidation together");
        db.ChangeTracker.Clear();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.Audit.Add(new() { Actor = "fixture", Action = "Location.Approved", Detail = "rollback" });
            before = cache.Snapshot();
            await db.SaveChangesAsync();
            Check(await Version() == 2 && !before.Changed.HasChanged, "revision changes inside explicit transaction without premature local invalidation");
            await transaction.RollbackAsync();
        }
        Check(await Version() == 1 && await db.Audit.CountAsync() == 1 && !before.Changed.HasChanged, "rollback preserves both data and shared revision atomically");
        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM PublicCatalogRevisions");
        db.Audit.Add(new() { Actor = "fixture", Action = "Location.Approved", Detail = "must rollback" });
        try { await db.SaveChangesAsync(); throw new Exception("Expected missing revision rejection"); }
        catch (InvalidOperationException) { }
        Check(await db.Audit.CountAsync() == 1, "failed revision update rolls back the data save");
        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlRawAsync("INSERT INTO PublicCatalogRevisions VALUES (1,1)");
        db.Audit.Add(new() { Actor = "fixture", Action = "UnrelatedAudit", Detail = "no change" });
        await db.SaveChangesAsync();
        Check(await Version() == 1, "unrelated audit writes do not advance public data revision");

        var serverSourceController = new HiringPs.Web.Areas.Admin.Controllers.SourcesController(db, new UrlPolicy(), Config("Server"));
        var serverSetup = await serverSourceController.SuggestSource("https://example.test", 0, null!, default);
        Check(serverSetup is Microsoft.AspNetCore.Mvc.ObjectResult { StatusCode: 409 },
            "server source setup is rejected before resolving or launching a browser");
        var clock = new FixtureClock();
        var factoryCalls = 0;
        using var provider = new ServiceCollection().AddScoped(_ =>
        {
            factoryCalls++;
            return new HiringDb(options);
        }).BuildServiceProvider();
        var monitor = new PublicCatalogRevisionMonitor(provider.GetRequiredService<IServiceScopeFactory>(), cache,
            NullLogger<PublicCatalogRevisionMonitor>.Instance, clock);
        await monitor.RefreshAsync(default);
        before = cache.Snapshot();
        await db.Database.ExecuteSqlRawAsync("UPDATE PublicCatalogRevisions SET Version = Version + 1");
        await monitor.RefreshAsync(default);
        Check(factoryCalls == 1 && !before.Changed.HasChanged, "visits within five seconds reuse revision observation without another SQL query");
        clock.Now = clock.Now.AddSeconds(5);
        await monitor.RefreshAsync(default);
        Check(factoryCalls == 2 && before.Changed.HasChanged, "remote revision update invalidates web cache after polling interval");
        var web = WebApplication.CreateBuilder();
        web.Logging.ClearProviders();
        web.WebHost.UseUrls("http://127.0.0.1:0");
        web.Services.AddSingleton(cache);
        web.Services.AddSingleton(monitor);
        web.Services.AddOutputCache(PublicCatalogCaching.Configure);
        await using (var app = web.Build())
        {
            app.UseOutputCache();
            var renders = 0;
            app.MapGet("/fixture", () => (++renders).ToString()).CacheOutput(SiteCache.Listing);
            await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            var html = await client.GetStringAsync("/fixture");
            Check(await client.GetStringAsync("/fixture") == html && renders == 1, "server output cache remains effective without a data change");
            await db.Database.ExecuteSqlRawAsync("UPDATE PublicCatalogRevisions SET Version = Version + 1");
            Check(await client.GetStringAsync("/fixture") == html, "remote changes respect the five-second polling window");
            clock.Now = clock.Now.AddSeconds(5);
            Check(await client.GetStringAsync("/fixture") != html && renders == 2,
                "remote database change refreshes an actual HTTP response through production cache policy");
            await app.StopAsync();
        }
        using var model = new DesignTimeFactory().CreateDbContext([]);
        Check(!model.Database.HasPendingModelChanges(), "EF model and new migration snapshot agree");
        Console.WriteLine($"Split runtime: {count} checks passed using isolated SQLite and no external providers.");
        async Task<long> Version() => await db.PublicCatalogRevisions.AsNoTracking().Select(x => x.Version).SingleAsync();
    }
    private sealed class FixtureClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
