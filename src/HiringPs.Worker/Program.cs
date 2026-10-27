using Microsoft.AspNetCore.DataProtection;
// تهيئة عامل الجمع المستقل واختيار وضع التشغيل المطلوب من وسائط الأمر.
using HiringPs.Core;

using Microsoft.EntityFrameworkCore;

CollectorCommandOptions command;
try { command = CollectorArguments.Parse(args); }
catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; return; }
if (command.Help)
{
    Console.WriteLine("Local collector: --collect [--source ID] [--checks] [--settings FILE] [--environment NAME]");
    Console.WriteLine("Checks only: --checks [--settings FILE]. Other diagnostics: --pdf-check, --pdf-read FILE, --probe, --install-browser.");
    Console.WriteLine("--once is an alias for a complete manual --collect round. No command starts a background service.");
    return;
}
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = [], ContentRootPath = AppContext.BaseDirectory, EnvironmentName = command.EnvironmentName,
});
if (command.SettingsFile is { } settings)
{
    builder.Configuration.AddJsonFile(settings, optional: false, reloadOnChange: false);
    builder.Configuration.AddJsonFile(Path.Combine(Path.GetDirectoryName(settings)!, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: false);
    builder.Configuration.AddEnvironmentVariables();
}
builder.Configuration["Runtime:Mode"] = "Local";
PdfToolPaths.Configure(builder.Configuration);

// ========================================
// 1. Command Arguments
// ========================================
// PDF diagnostics are independent of SQL, AI credentials and the collection schedule.
if (command.PdfCheck || command.PdfRead is not null)
{
    try
    {
        var reader = new PdfTextReader(builder.Configuration);
        await reader.CheckSetupAsync(CancellationToken.None);
        if (command.PdfRead is { } pdfFile)
        {
            var file = new FileInfo(pdfFile);
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

bool isProbe = command.Probe;
bool installBrowser = command.InstallBrowser;
if (installBrowser)
{
    BrowserSettings.Configure(builder.Configuration);
    Environment.ExitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
    return;
}

// ========================================
// 2. Application Services
// ========================================
BrowserSettings.Configure(builder.Configuration);
builder.Services.AddSingleton<UrlPolicy>();
builder.Services.AddScoped<AttachmentReader>();
builder.Services.AddScoped<BrowserCollector>();
builder.Services.AddBackgroundDependencies();

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
    builder.Services.AddSingleton<PublicCatalogRevisionWriter>();
    builder.Services.AddSingleton<PublicCatalogTransactionInterceptor>();
    builder.Services.AddDbContext<HiringDb>((services, options) =>
    {
        options.UseSqlServer(connectionString);
        options.AddInterceptors(services.GetRequiredService<PublicCatalogTransactionInterceptor>());
    });
    // Keep collection queues and tool diagnostics available when AI is not configured.
    // AiExtractor leaves those items awaiting AI instead of stopping the whole service.
    // Processing Services
    builder.Services.AddScoped<OpportunityService>();
    builder.Services.AddScoped<OpportunityExtractionService>();
    builder.Services.AddScoped<SourceItemProcessingService>();
    builder.Services.AddScoped<DiscoveryBatchStore>();
    builder.Services.AddScoped<MembershipService>();
    builder.Services.AddScoped<MemberVerification>();
    builder.Services.AddScoped<PaymentReconciliation>();
    builder.Services.AddScoped<PaymentRecoveryService>();
    builder.Services.AddScoped<PaymentTraceWriter>();
    builder.Services.AddScoped<PaymentGatewayRegistry>();
    builder.Services.AddSingleton<PaymentEnvironment>();
    builder.Services.AddScoped<PaymentToolProbes>();
    builder.Services.AddSocialPublishing();
    builder.Services.AddHiringDataProtection();
    builder.Services.AddLahza();
    builder.Services.AddJawwalPay();
    builder
        .Services.AddHttpClient<TelegramClient>(client => client.Timeout = TimeSpan.FromSeconds(20))
        .RemoveAllLoggers();
    builder.Services.AddScoped<CollectionPipeline>();
    // Gemini HTTP Client
    builder.Services.AddHttpClient<AiExtractor>();
}

// Collection is explicitly started by the terminal command. No membership or publication hosted services run here.

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

// A manual job owns only its heartbeat and pending diagnostics. Server jobs are never started here.
await host.StartAsync();
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
using var job = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
var runtime = host.Services.GetRequiredService<ToolWorkerRuntime>();
var monitor = new ToolMonitor(host.Services.GetRequiredService<IServiceScopeFactory>(), runtime,
    builder.Configuration, host.Services.GetRequiredService<ILogger<ToolMonitor>>());
Task pulse = Task.CompletedTask;
var monitoringStarted = false;
try
{
    await using (var schema = host.Services.CreateAsyncScope())
    {
        var db = schema.ServiceProvider.GetRequiredService<HiringDb>();
        if ((await db.Database.GetPendingMigrationsAsync(job.Token)).Any())
            throw new InvalidOperationException("Apply database migrations through the Web project before running the local collector.");
        _ = await db.PublicCatalogRevisions.SingleAsync(x => x.Id == 1, job.Token);
    }
    await monitor.PublishStateAsync(job.Token);
    monitoringStarted = true;
    pulse = monitor.PulseAsync(job.Token);
    await LocalToolCheckRunner.DrainAsync(host.Services, job.Token);
    if (command.Checks)
    {
        await using var checkScope = host.Services.CreateAsyncScope();
        var queued = await ToolCheckQueue.EnqueueAsync(checkScope.ServiceProvider.GetRequiredService<HiringDb>(),
            builder.Configuration, ToolCheckKind.Standard, "Local terminal", job.Token);
        if (!queued) Console.WriteLine("A standard check is already queued or was requested recently; duplicate request skipped.");
        await LocalToolCheckRunner.DrainAsync(host.Services, job.Token);
    }
    if (command.Collect)
    {
        runtime.Begin();
        await using var scope = host.Services.CreateAsyncScope();
        var summary = await scope.ServiceProvider.GetRequiredService<CollectionPipeline>().CollectManualAsync(command.SourceId, job.Token);
        if (summary.FailedSources > 0) runtime.Fail(new InvalidOperationException("ManualSourceFailures"));
        else runtime.Succeed();
        Console.WriteLine($"Round finished: sources={summary.Sources}; failed={summary.FailedSources}; processed={summary.ProcessedItems}; pending AI/retries={summary.PendingItems}.");
        if (summary.FailedSources > 0) Environment.ExitCode = 2;
        await LocalToolCheckRunner.DrainAsync(host.Services, job.Token);
    }
}
catch (OperationCanceledException) when (job.IsCancellationRequested)
{
    Console.Error.WriteLine("Local collection cancelled; saved checkpoints and pending items remain in the database.");
    Environment.ExitCode = 130;
}
catch (Exception ex)
{
    runtime.Fail(ex);
    await OperationFailureRecorder.TryRecordAsync(host.Services.GetRequiredService<IServiceScopeFactory>(), OperationArea.Collection,
        "ManualRound", ex, CancellationToken.None);
    Console.Error.WriteLine(ToolFailureDetail.Describe(ex, builder.Configuration));
    Environment.ExitCode = 1;
}
finally
{
    await job.CancelAsync();
    try { await pulse; } catch (OperationCanceledException) { }
    runtime.Stop();
    try
    {
        using var finalPulse = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        if (monitoringStarted) await monitor.PublishStateAsync(finalPulse.Token);
    }
    catch (Exception ex) { Console.Error.WriteLine($"Final local worker heartbeat was not saved ({ex.GetType().Name})."); }
    await host.StopAsync();
}

public partial class Program;
