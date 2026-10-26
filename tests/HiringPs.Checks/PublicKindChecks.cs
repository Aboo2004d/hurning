using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

internal static class PublicKindChecks
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool value, string label)
        {
            if (!value) throw new Exception(label);
            Console.WriteLine("PASS " + label);
            count++;
        }
        var rows = new[] {
            new Opportunity { Kind = OpportunityKind.Job }, new Opportunity { Kind = OpportunityKind.Training },
            new Opportunity { Kind = OpportunityKind.Tender }, new Opportunity { Kind = OpportunityKind.ProcurementNotice }
        }.AsQueryable();
        foreach (var kind in new[] { OpportunityKind.Tender, OpportunityKind.ProcurementNotice })
        {
            var result = PublicListingFilters.Apply(rows, new(null, kind, null, null, null, null), new([], [], [], [])).ToList();
            Check(result.Count == 2 && result.Select(x => x.Kind).Distinct().Count() == 2,
                "combined listing retains both original card kinds: " + kind);
        }
        Check(PublicListingKinds.Tabs.Count == 3 && PublicListingKinds.Tabs.Contains(OpportunityKind.Training),
            "navigation has one combined procurement tab and a separate training tab");
        Check(PublicListingKinds.Apply(rows, OpportunityKind.Training).Single().Kind == OpportunityKind.Training,
            "training filter remains separate");

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var coherence = new PublicCatalogCache();
        var writes = new SaveFixtureInterceptor();
        await using var db = new StatsFixtureDb(new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection)
            .AddInterceptors(writes, new PublicCatalogTransactionInterceptor()).Options, coherence);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE Opportunities (Id INTEGER PRIMARY KEY, Kind INTEGER NOT NULL, Status INTEGER NOT NULL,
                Deadline TEXT NULL, NormalizedOrganization TEXT NOT NULL, CreatedAt INTEGER NOT NULL);
            """);
        foreach (var kind in Enum.GetValues<OpportunityKind>())
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Opportunities (Kind,Status,NormalizedOrganization,CreatedAt) VALUES ({(int)kind},{(int)PublicationStatus.Published},{"organization"},{DateTimeOffset.UtcNow.UtcTicks})");
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var catalog = new PublicCatalog(db, cache, coherence);
        var first = await catalog.StatsAsync(default);
        Check(first.Count(OpportunityKind.Training) == 1 && first.GroupCount(OpportunityKind.Tender) == 2 && first.Total == 4,
            "database counts training and combines tenders without double-counting total");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Opportunities (Kind,Status,NormalizedOrganization,CreatedAt) VALUES ({(int)OpportunityKind.Training},{(int)PublicationStatus.Published},{"organization"},{DateTimeOffset.UtcNow.UtcTicks})");
        Check(ReferenceEquals(first, await catalog.StatsAsync(default)), "unchanged data reuses cached aggregate");
        db.PublicCatalogChanged();
        var updated = await catalog.StatsAsync(default);
        Check(updated.Count(OpportunityKind.Training) == 2 && updated.Total == 5,
            "publishing invalidates the training aggregate for the next read");
        var before = coherence.Snapshot();
        db.OpportunityAccessRules.Add(new() { Kind = OpportunityKind.Training });
        await db.SaveChangesAsync();
        Check(before.Changed.HasChanged && coherence.Snapshot().Version != before.Version,
            "successful tracked save invalidates both data token and page version");
        db.ChangeTracker.Clear();
        before = coherence.Snapshot();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.OpportunityAccessRules.Add(new() { Kind = OpportunityKind.Job });
            await db.SaveChangesAsync();
            Check(!before.Changed.HasChanged, "explicit transaction does not invalidate before commit");
            await transaction.CommitAsync();
        }
        Check(before.Changed.HasChanged, "committing an explicit transaction invalidates catalog");
        db.ChangeTracker.Clear();
        before = coherence.Snapshot();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.OpportunityAccessRules.Add(new() { Kind = OpportunityKind.Tender });
            db.SaveChanges();
            await transaction.RollbackAsync();
        }
        Check(!before.Changed.HasChanged, "rolled-back changes do not invalidate cache");
        db.ChangeTracker.Clear();
        db.OpportunityAccessRules.Add(new() { Kind = OpportunityKind.Tender });
        writes.Fail = true;
        try { await db.SaveChangesAsync(); throw new Exception("expected failed save"); }
        catch (IOException) { }
        Check(!before.Changed.HasChanged, "failed save does not invalidate cache");
        db.ChangeTracker.Clear();
        writes.Fail = false;
        foreach (var state in new[] { EntityState.Added, EntityState.Modified, EntityState.Deleted })
        {
            before = coherence.Snapshot();
            var opportunity = new Opportunity { Id = state == EntityState.Added ? 0 : 99, Title = "fixture", Status = PublicationStatus.NeedsReview };
            db.Entry(opportunity).State = state;
            await db.SaveChangesAsync();
            Check(before.Changed.HasChanged, "opportunity mutation invalidates cached pages: " + state);
            db.ChangeTracker.Clear();
        }
        before = coherence.Snapshot();
        db.Audit.Add(new AuditEntry { Action = "Fixture.NoCatalogChange" });
        await db.SaveChangesAsync();
        Check(!before.Changed.HasChanged, "unrelated audit saves preserve catalog cache");
        var day = new DateOnly(2026, 10, 26);
        var calendarCache = new PublicCatalogCache(() => day);
        var yesterday = calendarCache.Snapshot();
        day = day.AddDays(1);
        Check(calendarCache.Snapshot().Version != yesterday.Version && yesterday.Changed.HasChanged,
            "Palestine calendar rollover invalidates cached deadlines");
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(coherence);
        builder.Services.AddOutputCache(PublicCatalogCaching.Configure);
        await using var app = builder.Build();
        app.UseOutputCache();
        var renders = 0;
        app.MapGet("/fixture", () => (++renders).ToString()).CacheOutput(SiteCache.Listing);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        var initialPage = await client.GetStringAsync("/fixture");
        Check(await client.GetStringAsync("/fixture") == initialPage && renders == 1,
            "public HTTP listing policy serves unchanged page from output cache");
        coherence.Invalidate();
        Check(await client.GetStringAsync("/fixture") != initialPage && renders == 2,
            "catalog mutation refreshes cached HTTP page immediately without waiting for expiration");
        await app.StopAsync();
        Console.WriteLine($"Public kinds: {count} checks passed.");
    }
}

internal sealed class StatsFixtureDb(DbContextOptions<HiringDb> options, PublicCatalogCache coherence)
    : HiringDb(options, publicCatalog: coherence)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Entity<Opportunity>().Property(x => x.CreatedAt)
            .HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
    }
}

internal sealed class SaveFixtureInterceptor : SaveChangesInterceptor
{
    public bool Fail { get; set; }
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (Fail) throw new IOException("fixture save failure");
        return InterceptionResult<int>.SuppressWithResult(1);
    }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) => new(SavingChanges(eventData, result));
}
