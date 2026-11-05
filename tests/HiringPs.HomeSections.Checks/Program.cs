using System.Data.Common;
using HiringPs.Core;
using HiringPs.Web.Controllers;
using HiringPs.Web.Models;
using HiringPs.Web.Services.PublicCatalog;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
foreach (var (value, expected) in new (string?, int)[] { (null,180),("60",60),("1",30),("900",300) })
{
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Ai:TimeoutSeconds"] = value }).Build();
    Check(AiRequestLimits.TimeoutSeconds(config) == expected, "AI diagnostic and request share timeout: " + expected);
}
using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False"); await connection.OpenAsync();
var aiConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Ai:ApiKey"]="fixture", ["Ai:Model"]="fixture" }).Build();
using var fakeAiClient = new HttpClient(new TimeoutHandler());
try { await new AiExtractor(fakeAiClient,aiConfig).ExtractAsync("fixture",default); throw new Exception("Expected AI timeout"); }
catch(AiRequestTimeoutException) { Check(true,"HTTP timeout is distinguished from generic network failure"); }
connection.CreateCollation("Latin1_General_100_BIN2", StringComparer.Ordinal.Compare);
var counter = new QueryCounter();
var options = new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection).AddInterceptors(counter).Options;
await using var db = new HomeFixtureDb(options);
var schema = db.Database.GenerateCreateScript();
foreach(System.Text.RegularExpressions.Match table in System.Text.RegularExpressions.Regex.Matches(schema, @"CREATE TABLE[\s\S]*?;"))
{
    await db.Database.ExecuteSqlRawAsync(table.Value);
}
var today = TextRules.PalestineToday();
db.Opportunities.Add(new Opportunity { Title="Fixture opportunity", Organization="Fixture organization", NormalizedOrganization="fixture", Status=PublicationStatus.Published, Kind=OpportunityKind.Job, Deadline=today.AddDays(2), CreatedAt=DateTimeOffset.UtcNow });
await db.SaveChangesAsync(); db.ChangeTracker.Clear();
using var cache = new MemoryCache(new MemoryCacheOptions());
var catalog = new PublicCatalog(db,cache); var reads = new PublicListingReadService(db,catalog);
await catalog.FiltersAsync(null,false,default); counter.Sql.Clear();
var result = await reads.ReadAsync(new(null,null,null,null,null,null),false,ListingSort.Newest,1,today,default);
Check(result?.Page.Items.Count == 1, "initial read returns opportunity");
Check(result!.ClosingSoon.Count == 0, "initial read does not fetch closing section");
Check(counter.Sql.Count == 2, "warm-filter initial read runs count and page queries only");
counter.Sql.Clear(); var closing = await reads.ClosingWeekAsync(today,default);
Check(closing.Count == 1 && counter.Sql.Count == 1, "closing section uses its own bounded query");
var stats = await catalog.StatsAsync(default);
db.Opportunities.Add(new Opportunity { Title="Second fixture opportunity", Organization="Second fixture organization", NormalizedOrganization="second", LocationSearchText="رام الله", Status=PublicationStatus.Published, Kind=OpportunityKind.Job, Deadline=today.AddDays(3), CreatedAt=DateTimeOffset.UtcNow });
await db.SaveChangesAsync(); db.ChangeTracker.Clear();
Check((await catalog.StatsAsync(default)).Total == stats.Total, "normal stats reads reuse their cache");
Check((await catalog.StatsAsync(default, refresh:true)).Total == 2, "manual stats refresh reads current rows");
Console.WriteLine("Home section backend checks passed");
if (args.Contains("--serve"))
{
    var repo = new DirectoryInfo(Directory.GetCurrentDirectory()); while (!File.Exists(Path.Combine(repo.FullName, "HiringPs.sln"))) repo = repo.Parent ?? throw new Exception("Repository not found");
    var contentRoot = Path.Combine(repo.FullName, "src/HiringPs.Web");
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath=contentRoot, WebRootPath=Path.Combine(contentRoot,"wwwroot"), ApplicationName=typeof(HomeController).Assembly.FullName });
    builder.Logging.ClearProviders(); builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Warning);
    builder.WebHost.UseUrls("http://127.0.0.1:5487");
    builder.Services.AddScoped<HiringDb>(_=>new HomeFixtureDb(options));
    builder.Services.AddMemoryCache(); builder.Services.AddSingleton<PublicCatalogCache>();
    builder.Services.AddScoped<PublicCatalog>(); builder.Services.AddScoped<PublicListingReadService>();
    builder.Services.AddScoped<MembershipService>(); builder.Services.AddScoped<SavedOpportunityService>(); builder.Services.AddScoped<PublicSitemapReadService>();
    builder.Services.AddSingleton<SiteSeo>(); builder.Services.AddLocalization(x=>x.ResourcesPath="Resources");
    builder.Services.AddControllersWithViews().AddApplicationPart(typeof(HomeController).Assembly);
    builder.Services.AddOutputCache(PublicCatalogCaching.Configure);
    await using var app = builder.Build();
    app.UseStaticFiles();
    app.Use(async (context,next)=>{var culture=new System.Globalization.CultureInfo(context.Request.Path.StartsWithSegments("/en")?"en":"ar");System.Globalization.CultureInfo.CurrentCulture=culture;System.Globalization.CultureInfo.CurrentUICulture=culture;await next();});
    app.UseOutputCache();
    app.MapControllerRoute("fixture","{culture=ar}/{controller=Home}/{action=Index}/{id?}");
    Console.WriteLine("FIXTURE http://127.0.0.1:5487/ar");
    await app.RunAsync();
}

sealed class HomeFixtureDb(DbContextOptions<HiringDb> options) : HiringDb(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        foreach(var entity in model.Model.GetEntityTypes()) foreach(var property in entity.GetProperties())
        {
            if (property.ClrType == typeof(DateTimeOffset)) property.SetValueConverter(new ValueConverter<DateTimeOffset,long>(x=>x.UtcTicks,x=>new DateTimeOffset(x,TimeSpan.Zero)));
            if (property.ClrType == typeof(DateTimeOffset?)) property.SetValueConverter(new ValueConverter<DateTimeOffset?,long?>(x=>x.HasValue?x.Value.UtcTicks:null,x=>x.HasValue?new DateTimeOffset(x.Value,TimeSpan.Zero):null));
            if (property.IsConcurrencyToken && property.ClrType == typeof(byte[])) property.IsNullable=true;
        }
    }
}
sealed class QueryCounter : DbCommandInterceptor
{
    public List<string> Sql {get;}=[];
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<DbDataReader> result,CancellationToken ct=default){Sql.Add(command.CommandText);return new(result);}
}
sealed class TimeoutHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) =>
        Task.FromException<HttpResponseMessage>(new TaskCanceledException("fixture timeout"));
}
