// تهيئة عامل الجمع المستقل واختيار وضع التشغيل المطلوب من وسائط الأمر.
using HiringPs.Core;
using HiringPs.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(
    new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory }
);

// ========================================
// 1. Command Arguments
// ========================================
// PDF diagnostics are independent of SQL, AI credentials and the collection schedule.
if (args.Contains("--pdf-check") || args.Contains("--pdf-read"))
{
    try
    {
        var reader = new PdfTextReader(builder.Configuration);
        await reader.CheckSetupAsync(CancellationToken.None);
        if (Array.IndexOf(args, "--pdf-read") is var index && index >= 0)
        {
            if (index + 1 >= args.Length)
                throw new ArgumentException("Specify a local PDF path after --pdf-read.");
            var file = new FileInfo(args[index + 1]);
            if (!file.Exists || file.Length > AttachmentReader.MaxBytes)
                throw new ArgumentException("PDF must exist and be at most 8 MB.");
            var result = await reader.ReadAsync(
                await File.ReadAllBytesAsync(file.FullName),
                CancellationToken.None
            );
            Console.WriteLine(
                $"PDF read: {result.Hash}; pages={result.Pages.Count}; temporary files deleted"
            );
            foreach (var page in result.Pages)
                Console.WriteLine(
                    $"Page {page.Page}: {page.Method}; confidence={page.Confidence}; characters={page.Text.Length}"
                );
        }
        else
            Console.WriteLine(
                "PDF setup passed: tools, OCR languages (when enabled), and temporary workspace access; no processed files are retained."
            );
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
    return;
}

bool isProbe = args.Contains("--probe");
bool isOnce = args.Contains("--once");
bool installBrowser = args.Contains("--install-browser");
if (installBrowser)
{
    BrowserSettings.Configure(builder.Configuration);
    Environment.ExitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
    return;
}

// ========================================
// 2. Application Services
// ========================================
builder.Services.AddSingleton<UrlPolicy>();
builder.Services.AddScoped<AttachmentReader>();
builder.Services.AddScoped<BrowserCollector>();

// ========================================
// 3. Database & Processing Services
// ========================================
if (!isProbe)
{
    var connectionString =
        builder.Configuration.GetConnectionString("Hiring")
        ?? throw new InvalidOperationException(
            "Missing ConnectionStrings:Hiring in appsettings.json."
        );
    builder.Services.AddDbContext<HiringDb>(options =>
    {
        options.UseSqlServer(connectionString);
    });
    // Keep collection queues and tool diagnostics available when AI is not configured.
    // AiExtractor leaves those items awaiting AI instead of stopping the whole service.
    // Processing Services
    builder.Services.AddScoped<OpportunityService>();
    builder.Services.AddScoped<MembershipService>();
    builder.Services.AddScoped<MemberVerification>();
    builder.Services.AddScoped<PaymentReconciliation>();
    builder.Services.AddScoped<PaymentGatewayRegistry>();
    builder.Services.AddScoped<TelegramCommands>();
    builder.Services.AddScoped<MemberNotifications>();
    builder.Services.AddHttpClient<TelegramClient>(client => client.Timeout = TimeSpan.FromSeconds(20)).RemoveAllLoggers();
    builder.Services.AddScoped<CollectionPipeline>();
    // Gemini HTTP Client
    builder.Services.AddHttpClient<AiExtractor>();
}

// ========================================
// 4. Windows Service
// ========================================
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = builder.Configuration["Service:Name"] ?? "HiringPsCollector";
});

// ========================================
// 5. Background Worker
// ========================================
if (!isOnce && !isProbe)
{
    builder.Services.AddSingleton<ToolWorkerRuntime>();
    builder.Services.AddHostedService<Worker>();
    builder.Services.AddHostedService<ToolMonitor>();
    builder.Services.AddHostedService<MembershipWorker>();
}

// ========================================
// 6. Build Host
// ========================================
using var host = builder.Build();

// ========================================
// 7. Probe Mode
// ========================================
if (isProbe)
{
    await using var scope = host.Services.CreateAsyncScope();
    var collector = scope.ServiceProvider.GetRequiredService<BrowserCollector>();
    var samples = new[]
    {
        new Source
        {
            Name = "Gpc probe",
            Url = "https://www.gpc.pna.ps/diwan/viewPublicVacancyList.gpc",
            Connector = ConnectorKind.Gpc,
            Enabled = true,
            UsageApproved = true,
        },
        new Source
        {
            Name = "PalCareer probe",
            Url = "https://palcareer.com/",
            Connector = ConnectorKind.PalCareer,
            Enabled = true,
            UsageApproved = true,
        },
        new Source
        {
            Name = "Birzeit tenders probe",
            Url = "https://ritaj.birzeit.edu/fin/tenders/public/view-tenders",
            Connector = ConnectorKind.BrowserSelectors,
            Enabled = true,
            UsageApproved = true,
            ConfigurationJson =
                "{\"ListingLinkSelector\":\"a[href*='view-one-tender']\",\"ContentSelector\":\"body\",\"MaxPages\":5}",
        },
        new Source
        {
            Name = "PRCS tenders probe",
            Url = "https://www.palestinercs.org/en/Category/50/tenders",
            Connector = ConnectorKind.BrowserSelectors,
            Enabled = true,
            UsageApproved = true,
            ConfigurationJson =
                "{\"ListingLinkSelector\":\"a[href*='/tenders/'][href$='.pdf']\",\"ContentSelector\":\"body\",\"MaxPages\":5}",
        },
    };
    foreach (
        var source in samples.Where(x =>
            (!args.Contains("--birzeit") || x.Name.Contains("Birzeit"))
            && (!args.Contains("--prcs") || x.Name.Contains("PRCS"))
        )
    )
    {
        try
        {
            var links = await collector.DiscoverAsync(source, CancellationToken.None);
            var sample = await collector.FetchAsync(source, links[0].Url, CancellationToken.None);
            Console.WriteLine(
                $"{source.Name}: discovered={links.Count}, "
                    + $"sampleCharacters={sample.Length}; "
                    + "diagnostic only, no storage or publication."
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{source.Name}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    return;
}

// ========================================
// 8. Run Once
// ========================================
if (isOnce)
{
    await using var scope = host.Services.CreateAsyncScope();
    await scope
        .ServiceProvider.GetRequiredService<CollectionPipeline>()
        .TickAsync(CancellationToken.None);
    return;
}

// ========================================
// 9. Run Windows Service
// ========================================
await host.RunAsync();

public partial class Program;
