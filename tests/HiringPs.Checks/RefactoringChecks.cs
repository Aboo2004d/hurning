using System.Data.Common;
using System.Globalization;
using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks read boundaries, SQL translation, public metadata and scoped DI without opening a database.</summary>
public static class RefactoringChecks
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool value, string name)
        {
            if (!value)
                throw new Exception("FAIL: " + name);
            passed++;
            Console.WriteLine("PASS: " + name);
        }

        var options = new DbContextOptionsBuilder<HiringDb>()
            .UseSqlServer("Server=127.0.0.1,1;Database=TranslationOnly;User Id=unused;Password=unused;TrustServerCertificate=True")
            .AddInterceptors(new BlockConnections())
            .Options;
        await using var db = new HiringDb(options);
        Check(!db.Database.HasPendingModelChanges(), "organized configurations still match the supplied migration snapshot");

        var queries = new Dictionary<string, IQueryable>
        {
            ["contact inbox"] = new ContactInboxService(db).Messages("unread"),
            ["reports"] = new ReportManagementService(db).Reports(),
            ["merge history"] = new MergeAdministrationService(db).History(42),
            ["notifications"] = new NotificationAdministrationService(db, new MembershipService(db)).Messages("owner", "failed"),
            ["bot inbox"] = new NotificationAdministrationService(db, new MembershipService(db)).PendingInbox(),
            ["source queue"] = new SourceItemAdministrationService(db).Queue(),
            ["run metrics"] = new AdminDashboardReadService(db).Runs(),
            ["AI requests"] = new AiUsageReadService(db).Requests(DateTimeOffset.UtcNow.AddDays(-1)),
            ["AI token totals"] = new AiUsageReadService(db).Totals(DateTimeOffset.UtcNow.AddDays(-1)),
            ["prompt history"] = new PromptAdministrationService(db).History(),
            ["member saved rows"] = new SavedOpportunityService(db).ForMember("owner"),
        };
        foreach (var (name, query) in queries)
            Check(query.ToQueryString().Contains("SELECT", StringComparison.Ordinal), name + " DTO projection translates to SQL");

        var today = TextRules.PalestineToday();
        var filters = new FilterValues(["محاسبة"], ["دوام كامل"], ["مبتدئ"], ["غزة"]);
        var filtered = PublicListingFilters.Apply(
            PublicOpportunityScope.Listing(db.Opportunities.AsNoTracking(), false, today),
            new("محاسب", OpportunityKind.Job, "غزة", "محاسبة", "دوام كامل", "مبتدئ"), filters);
        var cards = filtered.OrderByDescending(x => x.SortDate).ThenByDescending(x => x.Id)
            .Skip(12).Take(12).Select(PublicOpportunityProjection.Card).ToQueryString();
        Check(cards.Contains("OFFSET") && cards.Contains("SearchText") && cards.Contains("LocationSearchText"),
            "filtered public cards keep search, city and stable SQL pagination");
        Check(!cards.Contains("ReviewReason") && !cards.Contains("AdminOverride") && !cards.Contains("AttachmentsJson"),
            "card queries exclude internal review and attachment payload columns");

        // Each call must compile its real query before this interceptor rejects connection opening.
        // A translation failure is not accepted as a successful check.
        async Task Translates(Func<Task> read, string label)
        {
            try
            {
                await read();
            }
            catch (ConnectionBlocked)
            {
                Check(true, label + " translates before the deliberately blocked database connection");
                return;
            }
            throw new Exception("Query unexpectedly bypassed the connection guard: " + label);
        }
        using var statsCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        await Translates(async () => { _ = await new PublicCatalog(db, statsCache).StatsAsync(default); }, "home statistics per-kind aggregate");
        await Translates(async () => { _ = await new OpportunityDetailsReadService(db).ReadAsync(1, default); }, "details and source-link projection");
        await Translates(async () => { _ = await new OpportunityDetailsReadService(db).DatesAsync(1, default); }, "verified dates");
        await Translates(async () => { _ = await new SiteContentService(db).ReadAsync("about", default); }, "versioned page snapshot");
        await Translates(async () => { _ = await new MemberAccountReadService(db).ReadAsync("owner", default); }, "member access snapshot");
        await Translates(async () => { _ = await new PublicSitemapReadService(db).ActiveKindsAsync(today, default); }, "sitemap type aggregates");
        await Translates(async () => { _ = await new PublicSitemapReadService(db).PageAsync(today, 1, 50, default); }, "public-only sitemap rows");

        var listing = new ListingModel
        {
            Kind = OpportunityKind.Job,
            Query = "محاسب & مدقق",
            City = "غزة",
            Category = "محاسبة",
            Employment = "دوام كامل",
            Experience = "مبتدئ",
            Sort = ListingSort.Closing,
            Page = 3,
        };
        var link = FeedLinks.ForListing(listing);
        var feedValues = QueryHelpers.ParseQuery(link[link.IndexOf('?')..]);
        Check(link.StartsWith("/feeds/jobs.xml?", StringComparison.Ordinal) && feedValues["q"] == listing.Query
            && feedValues["city"] == listing.City && feedValues["employment"] == listing.Employment
            && feedValues["experience"] == listing.Experience && !feedValues.ContainsKey("page"),
            "RSS preserves encoded search and job filters while omitting page numbers");

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seo:PublicOrigin"] = "https://example.test",
        }).Build();
        var seo = new SiteSeo(config);
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.RouteValues["controller"] = "Opportunities";
        context.Request.RouteValues["action"] = "Details";
        var item = new OpportunityDetailsDto
        {
            Id = 42,
            Title = "فرصة محاسب",
            Kind = OpportunityKind.Job,
            Status = PublicationStatus.Published,
            Organization = "مؤسسة",
            Summary = "نص عام",
            Locations = "غزة",
        };
        var metadata = seo.Build(context.Request, item, "title", "description");
        Check(metadata.Canonical.Contains("/Opportunities/Details/42/", StringComparison.Ordinal)
            && metadata.Robots.StartsWith("index", StringComparison.Ordinal), "details DTO retains canonical and indexable metadata");
        context.Response.StatusCode = 403;
        Check(seo.Build(context.Request, item, "title", "description").Robots.StartsWith("noindex", StringComparison.Ordinal),
            "denied details never advertise indexable metadata");
        context.Response.StatusCode = 200;
        context.Request.RouteValues["controller"] = "Home";
        listing.Items = [item];
        listing.Query = listing.City = listing.Category = listing.Employment = listing.Experience = "";
        listing.Sort = ListingSort.Newest;
        listing.Page = 1;
        listing.Count = 1;
        var restricted = seo.Build(context.Request, listing, "title", "description");
        Check(!restricted.StructuredData.Contains("/Opportunities/Details/42/", StringComparison.Ordinal),
            "listing structured data omits restricted detail links");

        const string main = "Verified announcement text";
        Check(ContentFingerprint.Hash(main + "\nSOURCE LINKS:\nold") == ContentFingerprint.Hash(main + "\nSOURCE LINKS:\nnew"),
            "restored content fingerprint ignores rotating source-link lists");
        Check(ContentFingerprint.Matches(TextRules.Hash(main), main), "old complete-text fingerprints remain reusable");
        Check(PublicAttachmentLinks.Read("[\"https://example.test/a.pdf\",\"http://example.test/b.pdf\",\"https://example.test/a.pdf\"]").Count == 1,
            "public attachments remain distinct HTTPS links");
        Check(PublicAttachmentLinks.Read("bad json").Count == 0, "malformed historical attachment JSON is harmless");

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddMemoryCache();
        services.AddDbContext<HiringDb>(builder => builder.UseSqlServer("Server=127.0.0.1,1;Database=Unused;User Id=unused;Password=unused;TrustServerCertificate=True"));
        services.AddScoped<MembershipService>();
        services.AddScoped<MemberVerification>();
        services.AddHttpClient<TelegramClient>();
        services.AddAdministrationFeatures();
        services.AddPublicOpportunityFeatures();
        services.AddMemberWorkspaceFeatures();
        services.AddScoped<PublicCatalog>();
        services.AddScoped<HiringPs.Web.Services.PublicCatalog.PublicListingReadService>();
        services.AddScoped<HiringPs.Web.Services.Membership.MemberPreferencePageService>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        Check(scope.ServiceProvider.GetRequiredService<PromptAdministrationService>() is not null
            && scope.ServiceProvider.GetRequiredService<HiringPs.Web.Services.PublicCatalog.PublicListingReadService>() is not null
            && scope.ServiceProvider.GetRequiredService<HiringPs.Web.Services.Membership.MemberPreferencePageService>() is not null,
            "new administration, public and member service dependency graphs resolve without a database");
        Console.WriteLine($"Refactoring checks passed: {passed}");
    }

    private sealed class ConnectionBlocked : Exception;

    private sealed class BlockConnections : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
            throw new ConnectionBlocked();

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) => throw new ConnectionBlocked();
    }
}
