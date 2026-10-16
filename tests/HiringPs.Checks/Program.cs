using System.Net;
using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

if (args.Contains("--admin-pagination-check"))
{
    AdminPaginationChecks.Run();
    return;
}

if (args.Contains("--tools-check"))
{
    await ToolChecks.RunAsync();
    return;
}

if (args.Contains("--pdf-process-fixture"))
{
    await PdfChecks.ProcessFixtureAsync(args);
    return;
}
if (args.Contains("--pdf-check"))
{
    await PdfChecks.RunAsync();
    return;
}

if (args.Contains("--workflow-check"))
{
    await WorkflowChecks.RunAsync();
    return;
}
if (args.Contains("--workflow-sql-check"))
{
    await WorkflowSqlChecks.RunAsync();
    return;
}

if (args.Contains("--unrwa-probe"))
{
    var policy = new UrlPolicy();
    var found = await UnCareersFeed.DiscoverAsync(policy, CancellationToken.None);
    Console.WriteLine($"Official UNRWA Palestine RSS matches: {found.Count}");
    await using var collector = new BrowserCollector(
        policy,
        new ConfigurationBuilder().Build(),
        new AttachmentReader(policy)
    );
    var source = new Source
    {
        Url = UnCareersFeed.Url,
        Connector = ConnectorKind.UnrwaInspira,
        Enabled = true,
        UsageApproved = true,
    };
    foreach (var item in found.Take(2))
    {
        var bundle = await collector.FetchBundleAsync(source, item.Url, CancellationToken.None);
        if (
            !bundle.Text.Contains("UNRWA", StringComparison.OrdinalIgnoreCase)
            || bundle.Text.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
        )
            throw new Exception("Expected real UNRWA content");
        Console.WriteLine(
            $"PASS: Inspira {item.Id}: {bundle.Text.Length} chars. No data saved or published."
        );
    }

    return;
}

if (args.Contains("--navigation-check"))
{
    using var runtime = await Playwright.CreateAsync();
    await using var navigationBrowser = await runtime.Chromium.LaunchAsync(
        new() { Headless = true }
    );
    var navigationPage = await navigationBrowser.NewPageAsync();
    await navigationPage.RouteAsync(
        "https://navigation.test/**",
        async route =>
        {
            var path = new Uri(route.Request.Url).AbsolutePath;
            if (path == "/redirect")
                await route.FulfillAsync(
                    new()
                    {
                        Status = 403,
                        ContentType = "text/html",
                        Body =
                            "<html><body>Waiting<script>setTimeout(()=>location.href='/ok',500)</script></body></html>",
                    }
                );
            else if (path == "/ok")
                await route.FulfillAsync(
                    new()
                    {
                        Status = 200,
                        ContentType = "text/html",
                        Body = "<html><body>" + new string('x', 100) + "</body></html>",
                    }
                );
            else
                await route.FulfillAsync(
                    new()
                    {
                        Status = path == "/missing" ? 404 : 403,
                        ContentType = "text/html",
                        Body = "<html><title>403 ERROR</title><body>Request blocked</body></html>",
                    }
                );
        }
    );
    await BrowserNavigation.OpenAsync(
        navigationPage,
        "https://navigation.test/redirect",
        WaitUntilState.DOMContentLoaded,
        3,
        CancellationToken.None
    );
    if (!navigationPage.Url.EndsWith("/ok"))
        throw new Exception("Automatic navigation was not observed");
    Console.WriteLine("PASS: Initial 403 with automatic successful document navigation resumes.");
    foreach (var path in new[] { "/blocked", "/missing" })
    {
        try
        {
            await BrowserNavigation.OpenAsync(
                navigationPage,
                "https://navigation.test" + path,
                WaitUntilState.DOMContentLoaded,
                1,
                CancellationToken.None
            );
            throw new Exception("Expected HTTP rejection");
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains(path == "/blocked" ? "after waiting 1 seconds" : "404"))
        {
            Console.WriteLine("PASS: " + path + " remains rejected.");
        }
    }

    using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(700)))
    {
        try
        {
            await BrowserNavigation.OpenAsync(
                navigationPage,
                "https://navigation.test/blocked",
                WaitUntilState.DOMContentLoaded,
                20,
                cancellation.Token
            );
            throw new Exception("Expected cancellation");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("PASS: Forbidden wait honours cancellation.");
        }
    }

    if (args.Contains("--live"))
        foreach (
            var url in new[]
            {
                "https://www.unrwa.org/careers/vacancies",
                "https://careers.un.org/jobopening",
            }
        )
        {
            try
            {
                await BrowserNavigation.OpenAsync(
                    navigationPage,
                    url,
                    WaitUntilState.DOMContentLoaded,
                    20,
                    CancellationToken.None
                );
                Console.WriteLine("LIVE: accessible " + url);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine("LIVE: " + url + " => " + ex.Message);
            }
        }

    return;
}

var passed = 0;
void Check(bool condition, string label)
{
    if (!condition)
        throw new Exception("FAIL: " + label);
    passed++;
    Console.WriteLine("PASS: " + label);
}

System.Xml.Linq.XElement FeedItem(string id, string department, string station) =>
    new(
        "item",
        new System.Xml.Linq.XElement(
            "link",
            $"https://careers.un.org/jobSearchDescription/{id}?language=en"
        ),
        new System.Xml.Linq.XElement(
            "description",
            $"Department/Office : {department} <br> Duty Station : {station} <br> Staffing Exercise : undefined"
        )
    );
var fixtureFeed = new System.Xml.Linq.XDocument(
    new System.Xml.Linq.XElement(
        "rss",
        new System.Xml.Linq.XElement(
            "channel",
            FeedItem("123", "United Nations Relief and Works Agency (UNRWA)", "HEBRON"),
            FeedItem("124", "UNRWA", "HQ Amman"),
            FeedItem("125", "OCHA", "EAST JERUSALEM"),
            FeedItem("126", "UNRWA", "NABLUS")
        )
    )
);
var feedMatches = UnCareersFeed.Parse(fixtureFeed);
Check(
    feedMatches.Select(x => x.Id).SequenceEqual(new[] { "123", "126" })
        && feedMatches.All(x => x.Url.StartsWith("https://inspira.un.org/")),
    "UNRWA RSS filters employer and Palestine duty station before mapping official detail IDs"
);
Check(
    UnCareersFeed
        .Parse(
            new System.Xml.Linq.XDocument(
                new System.Xml.Linq.XElement("rss", new System.Xml.Linq.XElement("channel"))
            )
        )
        .Count == 0,
    "Valid RSS with no local matches is accepted without fabricated jobs"
);
var automatic = AutomaticSource.Discover(
    "<nav><a href='/jobs/admin'>Job administrator</a></nav><a href='/jobs'>Jobs</a><article>New vacancy with details and deadline for qualified applicants in Palestine <a href='/jobs/accountant'>Accountant vacancy</a></article><a href='https://other.org/jobs/x'>External job</a><a rel='next' href='?page=2'>2</a>",
    new Uri("https://example.org/list")
);
Check(
    automatic.Links.SequenceEqual(new[] { "https://example.org/jobs/accountant" })
        && automatic.Next == "https://example.org/list?page=2",
    "Automatic discovery finds details and next page, excludes navigation and external hosts"
);
Check(
    AutomaticSource
        .Discover(
            "<a href='/about'>About us</a><a href='/jobs'>Jobs</a>",
            new Uri("https://example.org/list")
        )
        .Links.Length == 0,
    "Automatic discovery does not guess generic links"
);
Check(
    !AutomaticSource
        .ContentHtml(
            "<main><nav>Unwanted links</nav><p>"
                + new string('a', 120)
                + "</p><script>bad()</script></main>"
        )
        .Contains("Unwanted"),
    "Automatic detail extraction removes navigation and scripts"
);
try
{
    AutomaticSource.ContentHtml("<body>Uncertain content</body>");
    throw new Exception("Expected uncertain content");
}
catch (ContentNeedsReviewException)
{
    Check(true, "Unknown detail layout requires review instead of whole body extraction");
}

Check(TextRules.CharacterCount("ا\u0654") == 1, "Summary counts graphemes, not code units");
Check(
    TextRules.CharacterCount(string.Concat(Enumerable.Repeat("👩‍💻", 201))) == 201,
    "Emoji sequences count as one character"
);
Check(
    UrlPolicy.IsPrivate(IPAddress.Parse("127.0.0.1"))
        && UrlPolicy.IsPrivate(IPAddress.Parse("10.2.3.4"))
        && UrlPolicy.IsPrivate(IPAddress.Parse("::ffff:192.168.1.2")),
    "SSRF private and mapped IPs blocked"
);
Check(!UrlPolicy.IsPrivate(IPAddress.Parse("8.8.8.8")), "Public IP allowed");
var schedule = new Source
{
    IntervalHours = 24,
    CollectionStartTime = TimeOnly.MinValue,
    ScheduleAnchorDate = new DateOnly(2026, 10, 12),
};
DateTimeOffset AtLocal(int hour, int minute = 0) =>
    new(
        TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2026, 10, 12, hour, minute, 0, DateTimeKind.Unspecified),
            SourceSchedule.Zone
        ),
        TimeSpan.Zero
    );
Check(
    TimeZoneInfo
        .ConvertTime(SourceSchedule.NextAfter(schedule, AtLocal(0, 12)), SourceSchedule.Zone)
        .TimeOfDay == TimeSpan.Zero,
    "24-hour midnight schedule does not drift with collection duration"
);
schedule.IntervalHours = 8;
Check(
    TimeZoneInfo
        .ConvertTime(SourceSchedule.NextAfter(schedule, AtLocal(8, 30)), SourceSchedule.Zone)
        .Hour == 16,
    "8-hour schedule preserves 00/08/16 slots"
);
schedule.CollectionStartTime = new TimeOnly(12, 0);
Check(
    TimeZoneInfo
        .ConvertTime(SourceSchedule.NextAfter(schedule, AtLocal(8, 30)), SourceSchedule.Zone)
        .Hour == 12,
    "Edited start time changes the anchored schedule"
);
Check(
    OpportunityService.DateEvidenceMatches("Closing date: 21/10/2026", new DateOnly(2026, 10, 21)),
    "Date evidence independently confirms numeric deadline"
);
Check(
    !OpportunityService.DateEvidenceMatches("Closing date: 21/10/2026", new DateOnly(2026, 10, 22)),
    "AI deadline mismatch rejected"
);
Check(
    OpportunityService.PotentialDuplicate(
        new()
        {
            Kind = OpportunityKind.Job,
            Title = "محاسب مشاريع",
            Organization = "أثر",
        },
        new()
        {
            Kind = OpportunityKind.Job,
            Title = "محاسب مشاريع",
            Organization = "اثر",
            Deadline = DateOnly.FromDateTime(DateTime.Today).AddDays(-20),
        }
    ),
    "Repost detection does not depend on a 48-hour window or matching deadline"
);
Check(
    !OpportunityService.PotentialDuplicate(
        new()
        {
            Kind = OpportunityKind.Job,
            Title = "محاسب مشاريع",
            Organization = "أثر",
            ReferenceNumber = "JOB-1",
        },
        new()
        {
            Kind = OpportunityKind.Job,
            Title = "محاسب مشاريع",
            Organization = "أثر",
            ReferenceNumber = "JOB-2",
        }
    ),
    "Different explicit references are not auto merged"
);
var pdfBuilder = new PdfDocumentBuilder();
var pdfPage = pdfBuilder.AddPage(PageSize.A4);
var pdfFont = pdfBuilder.AddStandard14Font(Standard14Font.Helvetica);
pdfPage.AddText(
    "Tender reference TEST-123. Applications close 21 October 2026. This is a test document.",
    12,
    new PdfPoint(20, 700),
    pdfFont
);
Check(
    AttachmentReader.ExtractText(pdfBuilder.Build()).Contains("TEST-123"),
    "PDF attachment extracts reference and text"
);
var emptyPdf = new PdfDocumentBuilder();
emptyPdf.AddPage(PageSize.A4);
try
{
    AttachmentReader.ExtractText(emptyPdf.Build());
    throw new Exception("Expected OCR review");
}
catch (ContentNeedsReviewException)
{
    Check(true, "Image/blank PDF does not fabricate OCR text");
}

try
{
    AttachmentReader.ExtractText(System.Text.Encoding.UTF8.GetBytes("<html>blocked</html>"));
    throw new Exception("Expected PDF validation");
}
catch (ContentNeedsReviewException)
{
    Check(true, "HTML response is not accepted as PDF");
}

var array = BrowserCollector.ExtractEmbeddedArray(
    "datastr : [{\"description\":\"quoted ] [ text\",\"vacancyNum\":\"8/2026\"}] other",
    "datastr"
);
Check(
    array.GetArrayLength() == 1 && array[0].GetProperty("vacancyNum").GetString() == "8/2026",
    "Embedded JSON handles brackets in strings"
);
var valid = new Opportunity
{
    Title = "محاسب",
    Organization = "جهة الاختبار",
    Locations = "غزة",
    Summary = "ملخص للاختبار",
    OriginalLanguage = "ar",
    Kind = OpportunityKind.Job,
};
Check(OpportunityService.Validate(valid).Count == 0, "Valid local opportunity accepted");
valid.Summary = new string('a', 201);
Check(
    !OpportunityService.Validate(valid).Any(x => x.Contains("200")),
    "Summary length remains an AI instruction, not a persistence restriction"
);
valid.Summary = "ملخص";
valid.Kind = OpportunityKind.Tender;
valid.EmploymentType = "Full time";
Check(
    OpportunityService.Validate(valid).Any(x => x.Contains("jobs only")),
    "Tender cannot inherit employment fields"
);
valid.EmploymentType = "";
valid.Locations = "London";
Check(
    OpportunityService.Validate(valid).Any(x => x.Contains("geographic")),
    "Foreign location requires review"
);
valid.ApplyUrl = "javascript:alert(1)";
Check(
    OpportunityService.Validate(valid).Any(x => x.Contains("application")),
    "Unsafe apply scheme rejected"
);
using var http = new HttpClient();
var config = new ConfigurationBuilder().Build();
try
{
    await new AiExtractor(http, config).ExtractAsync("source", CancellationToken.None);
    throw new Exception("Expected missing key");
}
catch (AiUnavailableException)
{
    Check(true, "Missing AI key pauses extraction without network request");
}

var databaseName = "HiringPs_Checks_" + Guid.NewGuid().ToString("N");
var connection =
    $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
var options = new DbContextOptionsBuilder<HiringDb>().UseSqlServer(connection).Options;
await using var db = new HiringDb(options);
await db.Database.MigrateAsync();
try
{
    var source = new Source { Name = "Check fixture", Url = "https://example.com" };
    db.Sources.Add(source);
    await db.SaveChangesAsync();
    var content = "محاسب جهة الاختبار غزة رقم TEST-1 آخر موعد للتقديم 21/10/2026";
    var item = new SourceItem
    {
        SourceId = source.Id,
        ExternalId = "check-1",
        Url = "https://example.com/job",
        Content = content,
        ContentHash = TextRules.Hash(content),
    };
    db.SourceItems.Add(item);
    await db.SaveChangesAsync();
    var e = new ExtractedOpportunity
    {
        ChildKey = "TEST-1",
        Kind = OpportunityKind.Job,
        Title = "محاسب",
        Organization = "جهة الاختبار",
        Locations = "غزة",
        LocationEvidence = "غزة",
        Summary = "إعداد التقارير المالية",
        OriginalLanguage = "ar",
        ReferenceNumber = "TEST-1",
        Deadline = new DateOnly(2026, 10, 21),
        DeadlineEvidence = "آخر موعد للتقديم 21/10/2026",
    };
    var service = new OpportunityService(db);
    await service.ApplyExtractionAsync(item, new(true, [e]), CancellationToken.None);
    Check(
        await db.Opportunities.CountAsync() == 1 && await db.SourceLinks.CountAsync() == 1,
        "SQL stores opportunity and source atomically"
    );
    await service.ApplyExtractionAsync(item, new(true, [e]), CancellationToken.None);
    Check(
        await db.Opportunities.CountAsync() == 1 && await db.SourceLinks.CountAsync() == 1,
        "Reprocessing same child is idempotent"
    );
    e.Summary = "ملخص محدث";
    await service.ApplyExtractionAsync(item, new(true, [e]), CancellationToken.None);
    Check(
        (await db.Opportunities.SingleAsync()).Summary == "ملخص محدث",
        "Changed content updates existing record"
    );
    var second = new SourceItem
    {
        SourceId = source.Id,
        ExternalId = "check-2",
        Url = "https://example.com/repost",
        Content = content,
    };
    db.SourceItems.Add(second);
    await db.SaveChangesAsync();
    await service.ApplyExtractionAsync(second, new(true, [e]), CancellationToken.None);
    Check(
        await db.Opportunities.CountAsync() == 1 && await db.SourceLinks.CountAsync() == 2,
        "Same unique reference links second source without duplicate"
    );
    var o = await db.Opportunities.SingleAsync();
    o.AdminOverride = true;
    o.Status = PublicationStatus.Hidden;
    await db.SaveChangesAsync();
    e.Summary = "must not overwrite";
    await service.ApplyExtractionAsync(item, new(true, [e]), CancellationToken.None);
    Check(
        o.Status == PublicationStatus.Hidden && o.Summary == "ملخص محدث",
        "Admin hide/edit preserved on reprocessing"
    );
    db.Opportunities.AddRange(
        new Opportunity
        {
            Title = "No deadline",
            Organization = "Check",
            Locations = "Gaza",
            Summary = "Test",
            Status = PublicationStatus.Published,
        },
        new Opportunity
        {
            Title = "Expired",
            Organization = "Check",
            Locations = "Gaza",
            Summary = "Test",
            Status = PublicationStatus.Published,
            Deadline = TextRules.PalestineToday().AddDays(-1),
        }
    );
    await db.SaveChangesAsync();
    await service.ArchiveExpiredAsync(CancellationToken.None);
    Check(
        await db.Opportunities.AnyAsync(x =>
            x.Title == "No deadline" && x.Status == PublicationStatus.Published
        ),
        "Unknown deadline stays published"
    );
    Check(
        await db.Opportunities.AnyAsync(x =>
            x.Title == "Expired" && x.Status == PublicationStatus.Archived
        ),
        "Explicit elapsed deadline archived"
    );
    await using (var first = new SqlRunLock(connection))
    {
        Check(
            await first.AcquireAsync(CancellationToken.None),
            "First SQL session acquires pipeline lock"
        );
        await using var secondLock = new SqlRunLock(connection);
        Check(
            !await secondLock.AcquireAsync(CancellationToken.None),
            "Concurrent SQL session cannot collect"
        );
        await first.VerifyAsync(CancellationToken.None);
    }

    await using (var restart = new SqlRunLock(connection))
    {
        Check(
            await restart.AcquireAsync(CancellationToken.None),
            "Lock released on session disposal; restart can resume"
        );
    }

    source.Enabled = true;
    source.UsageApproved = true;
    source.NextDueAt = DateTimeOffset.UtcNow.AddDays(1);
    item.Status = ProcessingStatus.AwaitingAi;
    item.RetryAt = null;
    await db.SaveChangesAsync();
    var pipelineConfig = new ConfigurationBuilder()
        .AddInMemoryCollection(
            new Dictionary<string, string?> { { "ConnectionStrings:Hiring", connection } }
        )
        .Build();
    var registrations = new ServiceCollection();
    registrations.AddSingleton<IConfiguration>(pipelineConfig);
    registrations.AddLogging();
    CheckServices.Register(registrations, pipelineConfig);
    await using var provider = registrations.BuildServiceProvider();
    await using (var pipelineScope = provider.CreateAsyncScope())
    {
        await pipelineScope
            .ServiceProvider.GetRequiredService<CollectionPipeline>()
            .TickAsync(CancellationToken.None);
    }

    db.ChangeTracker.Clear();
    var queued = await db.SourceItems.FindAsync(item.Id);
    Check(
        queued?.Status == ProcessingStatus.AwaitingAi && queued.RetryAt > DateTimeOffset.UtcNow,
        "Worker persists AI quota wait without publishing guessed data"
    );
    Check(
        await db.Heartbeats.AnyAsync(x => x.State == "Idle"),
        "Worker heartbeat is created with SQL-generated identity"
    );
    var training = new Opportunity
    {
        Kind = OpportunityKind.Training,
        Title = "تدريب مهارات برمجة",
        Organization = "مؤسسة أثر",
        Locations = "القدس",
        Summary = "فرصة تدريب للاختبار",
        Status = PublicationStatus.Published,
    };
    db.Opportunities.Add(training);
    await db.SaveChangesAsync();
    Check(
        training.NormalizedOrganization == "مؤسسة اثر",
        "Organization normalization saved for matching index"
    );
    var trainingItem = new SourceItem
    {
        SourceId = source.Id,
        ExternalId = "training-repost",
        Url = "https://example.com/training",
        Content = "تدريب مهارات برمجة مؤسسة اثر القدس",
    };
    db.SourceItems.Add(trainingItem);
    await db.SaveChangesAsync();
    await service.ApplyExtractionAsync(
        trainingItem,
        new(
            true,
            [
                new()
                {
                    ChildKey = "training",
                    Kind = OpportunityKind.Training,
                    Title = "تدريب مهارات برمجة",
                    Organization = "مؤسسة اثر",
                    Locations = "القدس",
                    LocationEvidence = "القدس",
                    Summary = "تدريب جديد للاختبار",
                },
            ]
        ),
        CancellationToken.None
    );
    Check(
        (
            await db
                .SourceLinks.Include(x => x.Opportunity)
                .SingleAsync(x => x.SourceItemId == trainingItem.Id)
        )
            .Opportunity
            .Status == PublicationStatus.NeedsReview,
        "Normalized employer alias and unknown reference route repost to review"
    );
    var roles = new SourceItem
    {
        SourceId = source.Id,
        ExternalId = "multiple-roles",
        Url = "https://example.com/roles",
        Content = "Regional Officer Finance Officer Example Org Gaza ROLE-A ROLE-B",
    };
    db.SourceItems.Add(roles);
    await db.SaveChangesAsync();
    var roleA = new ExtractedOpportunity
    {
        ChildKey = "a",
        Kind = OpportunityKind.Job,
        Title = "Regional Officer",
        Organization = "Example Org",
        Locations = "Gaza",
        LocationEvidence = "Gaza",
        Summary = "Regional coordination",
        OriginalLanguage = "en",
        ReferenceNumber = "ROLE-A",
    };
    var roleB = new ExtractedOpportunity
    {
        ChildKey = "b",
        Kind = OpportunityKind.Job,
        Title = "Finance Officer",
        Organization = "Example Org",
        Locations = "Gaza",
        LocationEvidence = "Gaza",
        Summary = "Financial reporting",
        OriginalLanguage = "en",
        ReferenceNumber = "ROLE-B",
    };
    await service.ApplyExtractionAsync(roles, new(true, [roleA, roleB]), CancellationToken.None);
    await service.ApplyExtractionAsync(roles, new(true, [roleA]), CancellationToken.None);
    Check(
        (
            await db
                .SourceLinks.Include(x => x.Opportunity)
                .SingleAsync(x => x.SourceItemId == roles.Id && x.ChildKey == "b")
        )
            .Opportunity
            .Status == PublicationStatus.NeedsReview,
        "Removed role in revised source is reviewed instead of silently kept published"
    );
    await using var left = new HiringDb(options);
    await using var right = new HiringDb(options);
    var firstCopy = await left.Opportunities.FindAsync(training.Id);
    var staleCopy = await right.Opportunities.FindAsync(training.Id);
    firstCopy!.Summary = "تعديل أول";
    await left.SaveChangesAsync();
    staleCopy!.Summary = "تعديل قديم";
    try
    {
        await right.SaveChangesAsync();
        throw new Exception("Expected stale edit conflict");
    }
    catch (DbUpdateConcurrencyException)
    {
        Check(true, "SQL rowversion rejects stale concurrent admin edits");
    }

    db.ChangeTracker.Clear();
    var target = new Source
    {
        Name = "Manual target",
        Url = "https://example.com/target",
        Enabled = true,
        UsageApproved = true,
        ApprovalNote = "Fixture only",
        NextDueAt = DateTimeOffset.UtcNow.AddDays(1),
    };
    var unrelated = new Source
    {
        Name = "Unrelated due source",
        Url = "https://example.com/unrelated",
        Enabled = true,
        UsageApproved = true,
        ApprovalNote = "Fixture only",
        NextDueAt = DateTimeOffset.UtcNow.AddHours(-1),
    };
    db.Sources.AddRange(target, unrelated);
    await db.SaveChangesAsync();
    var originalNext = target.NextDueAt;
    var unrelatedDue = unrelated.NextDueAt;
    db.Set<SourceCollectionRequest>().Add(new() { SourceId = target.Id });
    await db.SaveChangesAsync();
    var seen = new List<int>();
    var mockServices = new ServiceCollection();
    mockServices.AddSingleton<IConfiguration>(pipelineConfig);
    mockServices.AddLogging();
    CheckServices.Register(mockServices, pipelineConfig);
    mockServices.AddScoped<BrowserCollector>(sp => new FixtureCollector(
        sp.GetRequiredService<UrlPolicy>(),
        pipelineConfig,
        sp.GetRequiredService<AttachmentReader>(),
        seen
    ));
    await using var mockProvider = mockServices.BuildServiceProvider();
    await using (var round = mockProvider.CreateAsyncScope())
    {
        await round
            .ServiceProvider.GetRequiredService<CollectionPipeline>()
            .TickAsync(CancellationToken.None);
    }

    db.ChangeTracker.Clear();
    Check(
        seen.SequenceEqual(new[] { target.Id }),
        "Manual collection discovers only requested source, even when another source is due"
    );
    Check(
        (await db.Sources.FindAsync(unrelated.Id))!.NextDueAt == unrelatedDue,
        "Manual collection leaves unrelated source schedule untouched"
    );
    Check(
        (await db.Sources.FindAsync(target.Id))!.NextDueAt == originalNext,
        "Manual collection does not reset regular future schedule"
    );
    Check(
        (await db.Set<SourceCollectionRequest>().FindAsync(target.Id))!.Status == "Completed",
        "Manual request completes while AI-unavailable items stay queued"
    );
    var queuedTarget = await db.SourceItems.SingleAsync(x => x.SourceId == target.Id);
    Check(
        queuedTarget.Status == ProcessingStatus.AwaitingAi,
        "Source-specific manual run keeps raw content without guessing or publishing"
    );
    var resume = await db.Set<SourceCollectionRequest>().FindAsync(target.Id);
    resume!.Status = "Running";
    resume.DiscoveryFinished = true;
    queuedTarget.Status = ProcessingStatus.Discovered;
    queuedTarget.RetryAt = null;
    await db.SaveChangesAsync();
    await using (var round = mockProvider.CreateAsyncScope())
    {
        await round
            .ServiceProvider.GetRequiredService<CollectionPipeline>()
            .TickAsync(CancellationToken.None);
    }

    db.ChangeTracker.Clear();
    Check(
        seen.Count == 1
            && (await db.Set<SourceCollectionRequest>().FindAsync(target.Id))!.Status
                == "Completed",
        "Restart resumes a manual batch without rediscovering or collecting another source"
    );
}
finally
{
    if (
        db.Database.GetDbConnection().Database == databaseName
        && databaseName.StartsWith("HiringPs_Checks_", StringComparison.Ordinal)
    )
        await db.Database.EnsureDeletedAsync();
}

Console.WriteLine(
    $"{passed} checks passed, including real SQL Server integration. Temporary fixture database removed."
);

class FixtureCollector(
    UrlPolicy policy,
    IConfiguration configuration,
    AttachmentReader reader,
    List<int> seen
) : BrowserCollector(policy, configuration, reader)
{
    public override Task<List<(string Id, string Url)>> DiscoverAsync(
        Source source,
        CancellationToken ct
    )
    {
        seen.Add(source.Id);
        return Task.FromResult(new List<(string, string)> { ("fixture-1", source.Url + "/item") });
    }

    public override Task<ContentBundle> FetchBundleAsync(
        Source source,
        string url,
        CancellationToken ct
    ) =>
        Task.FromResult(
            new ContentBundle(
                "Fixture collection data for a local Gaza opportunity. This is not a real advertisement.",
                []
            )
        );
}
