using HiringPs.Core;
using HiringPs.Worker;
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings{Args=args,ContentRootPath=AppContext.BaseDirectory});
if (builder.Environment.IsDevelopment()) builder.Configuration.AddUserSecrets<Program>(optional: true);
if (args.Contains("--install-browser")) { Environment.ExitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]); return; }
if(args.Contains("--probe")){builder.Services.AddSingleton<UrlPolicy>();builder.Services.AddScoped<AttachmentReader>();builder.Services.AddScoped<BrowserCollector>();}
else builder.Services.AddHiring(builder.Configuration);
builder.Services.AddWindowsService(o => o.ServiceName = builder.Configuration["Service:Name"]??"HiringPsCollector");
if (!args.Contains("--once") && !args.Contains("--probe")) builder.Services.AddHostedService<Worker>();
using var host = builder.Build();
if (args.Contains("--probe"))
{
    await using var scope = host.Services.CreateAsyncScope(); var collector = scope.ServiceProvider.GetRequiredService<BrowserCollector>();
    var samples=new[] { new Source { Name = "Gpc probe", Url = "https://www.gpc.pna.ps/diwan/viewPublicVacancyList.gpc", Connector = ConnectorKind.Gpc, Enabled = true, UsageApproved = true }, new Source { Name = "PalCareer probe", Url = "https://palcareer.com/", Connector = ConnectorKind.PalCareer, Enabled = true, UsageApproved = true },new Source{Name="Birzeit tenders probe",Url="https://ritaj.birzeit.edu/fin/tenders/public/view-tenders",Connector=ConnectorKind.BrowserSelectors,Enabled=true,UsageApproved=true,ConfigurationJson="{\"ListingLinkSelector\":\"a[href*='view-one-tender']\",\"ContentSelector\":\"body\",\"MaxPages\":5}"}};
    samples=samples.Append(new Source{Name="PRCS tenders probe",Url="https://www.palestinercs.org/en/Category/50/tenders",Connector=ConnectorKind.BrowserSelectors,Enabled=true,UsageApproved=true,ConfigurationJson="{\"ListingLinkSelector\":\"a[href*='/tenders/'][href$='.pdf']\",\"ContentSelector\":\"body\",\"MaxPages\":5}"}).ToArray();
    foreach (var source in samples.Where(x=>(!args.Contains("--birzeit")||x.Name.Contains("Birzeit"))&&(!args.Contains("--prcs")||x.Name.Contains("PRCS"))))
    {
        try { var links = await collector.DiscoverAsync(source, CancellationToken.None); var sample = await collector.FetchAsync(source, links[0].Url, CancellationToken.None); Console.WriteLine($"{source.Name}: discovered={links.Count}, sampleCharacters={sample.Length}; diagnostic only, no storage or publication."); }
        catch (Exception ex) { Console.WriteLine($"{source.Name}: {ex.GetType().Name}: {ex.Message}"); }
    }
    return;
}
if (args.Contains("--once")) { await using var scope = host.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<CollectionPipeline>().TickAsync(CancellationToken.None); return; }
await host.RunAsync();
public partial class Program;
