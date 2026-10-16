using System.Net;
using System.Text;
using HiringPs.Core;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class WorkflowSqlChecks
{
    public static async Task RunAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("HIRING_TEST_SQL");
        if (string.IsNullOrWhiteSpace(supplied))
            throw new InvalidOperationException(
                "HIRING_TEST_SQL is required for the isolated workflow SQL checks. No database was changed."
            );
        var name = "HiringPs_WorkflowChecks_" + Guid.NewGuid().ToString("N");
        var builder = new SqlConnectionStringBuilder(supplied) { InitialCatalog = name };
        var connection = builder.ConnectionString;
        var options = new DbContextOptionsBuilder<HiringDb>().UseSqlServer(connection).Options;
        await using var db = new HiringDb(options);
        var folder = Path.Combine(
            Path.GetTempPath(),
            "hiring-workflow-checks-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(folder);
        var passed = 0;
        void Check(bool condition, string label)
        {
            if (!condition)
                throw new Exception(label);
            passed++;
            Console.WriteLine("PASS " + label);
        }
        try
        {
            await db.Database.MigrateAsync();
            var initial = await db.PromptVersions.SingleAsync(x => x.Id == 1);
            Check(
                initial.LegacySnapshot == PromptDefaults.Legacy.ReplaceLineEndings("\n")
                    && initial.Render() == initial.FinalText,
                "migration preserves original and effective prompt snapshots"
            );
            var source = new Source
            {
                Name = "Workflow fixture",
                Url = "https://example.com",
                Enabled = true,
                UsageApproved = true,
                NextDueAt = DateTimeOffset.UtcNow.AddDays(1),
            };
            db.Sources.Add(source);
            await db.SaveChangesAsync();
            SourceItem Item(string id) =>
                new()
                {
                    SourceId = source.Id,
                    ExternalId = id,
                    Url = "https://example.com/" + id,
                    Content = "Fixture role Fixture org غزة",
                    ExtractionPromptVersionId = 1,
                    RequestedPromptVersionId = 1,
                    Status = ProcessingStatus.NeedsReview,
                };
            Opportunity Opportunity(string title, PublicationStatus status, bool manual = false) =>
                new()
                {
                    Title = title,
                    Organization = "Fixture org",
                    Summary = "Fixture summary",
                    Locations = "Ramallah",
                    Status = status,
                    AdminOverride = manual,
                    PromptVersionId = 1,
                };
            var published = Opportunity("published", PublicationStatus.Published);
            var archived = Opportunity("archived", PublicationStatus.Archived);
            var review = Opportunity("review", PublicationStatus.NeedsReview);
            var manual = Opportunity("manual", PublicationStatus.NeedsReview, true);
            var list = new[] { published, archived, review, manual };
            foreach (var opportunity in list)
                db.SourceLinks.Add(
                    new()
                    {
                        Opportunity = opportunity,
                        SourceItem = Item(opportunity.Title),
                        ChildKey = opportunity.Title,
                    }
                );
            await db.SaveChangesAsync();
            Check(
                review.LocationSearchText.Contains("رام الله")
                    && review.SearchText.Contains("ramallah"),
                "search-only fields contain Arabic and English aliases"
            );
            var next = new PromptVersion
            {
                Template = initial.Template,
                SummaryLimit = 150,
                Note = "Fixture version",
                CreatedBy = "Checks",
            };
            next.FinalText = next.Compose();
            db.PromptVersions.Add(next);
            await db.SaveChangesAsync();
            var settings = await db.WorkflowSettings.SingleAsync();
            settings.PendingPromptVersionId = next.Id;
            await db.SaveChangesAsync();
            await PromptWorkflow.ActivateAsync(db, CancellationToken.None);
            db.ChangeTracker.Clear();
            var items = await db.SourceItems.ToDictionaryAsync(x => x.ExternalId);
            Check(
                items["review"].Status == ProcessingStatus.AwaitingAi
                    && items["review"].RequestedPromptVersionId == next.Id,
                "new prompt queues unpublished unprotected data"
            );
            Check(
                items["published"].RequestedPromptVersionId == 1
                    && items["archived"].RequestedPromptVersionId == 1
                    && items["manual"].RequestedPromptVersionId == 1,
                "activation leaves published, archived and manual results alone"
            );
            var duplicate = Opportunity("duplicate", PublicationStatus.Published);
            var target = Opportunity("target", PublicationStatus.Published);
            var link = new OpportunitySourceLink
            {
                Opportunity = duplicate,
                SourceItem = Item("merge-source"),
                ChildKey = "duplicate",
            };
            db.Opportunities.Add(target);
            db.SourceLinks.Add(link);
            await db.SaveChangesAsync();
            Check(
                await MergeHistory.MergeAsync(
                    db,
                    duplicate.Id,
                    target.Id,
                    "Checks",
                    CancellationToken.None
                ),
                "merge stores a restoration snapshot"
            );
            var operation = await db.MergeOperations.OrderByDescending(x => x.Id).FirstAsync();
            Check(
                link.OpportunityId == target.Id && duplicate.Status == PublicationStatus.Hidden,
                "merge moves source links and hides the duplicate"
            );
            Check(
                await MergeHistory.MergeAsync(
                    db,
                    target.Id,
                    published.Id,
                    "Checks",
                    CancellationToken.None
                ),
                "a merge target can be merged onward without losing its restoration history"
            );
            var chain = await db.MergeOperations.OrderByDescending(x => x.Id).FirstAsync();
            Check(
                !await MergeHistory.UndoAsync(db, operation.Id, "Checks", CancellationToken.None),
                "an earlier merge cannot be undone before the later dependent merge"
            );
            Check(
                await MergeHistory.UndoAsync(db, chain.Id, "Checks", CancellationToken.None),
                "undoing the later merge restores the earlier merge's rowversion snapshot"
            );
            Check(
                await MergeHistory.UndoAsync(db, operation.Id, "Checks", CancellationToken.None),
                "undo restores the snapshot"
            );
            Check(
                link.OpportunityId == duplicate.Id
                    && duplicate.Status == PublicationStatus.Published
                    && !duplicate.AdminOverride
                    && !target.AdminOverride,
                "undo restores links, status and prior override flags"
            );
            Check(
                await MergeHistory.MergeAsync(
                    db,
                    duplicate.Id,
                    target.Id,
                    "Checks",
                    CancellationToken.None
                ),
                "a restored pair may be merged again"
            );
            operation = await db.MergeOperations.OrderByDescending(x => x.Id).FirstAsync();
            target.Summary = "A later administrator edit";
            await db.SaveChangesAsync();
            Check(
                !await MergeHistory.UndoAsync(db, operation.Id, "Checks", CancellationToken.None),
                "undo refuses to overwrite a later edit"
            );

            var retention = await db.RetentionPolicies.SingleAsync();
            retention.RevisionDays = 7;
            retention.DiagnosticDays = 7;
            db.Revisions.Add(
                new()
                {
                    SourceItemId = items["review"].Id,
                    Content = "obsolete",
                    ContentHash = "old",
                    CapturedAt = DateTimeOffset.UtcNow.AddDays(-20),
                }
            );
            db.Revisions.Add(
                new()
                {
                    SourceItemId = items["review"].Id,
                    Content = "recent",
                    ContentHash = "new",
                }
            );
            await db.SaveChangesAsync();
            var oldDiagnostic = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".json");
            var unrelated = Path.Combine(folder, "not-a-diagnostic.json");
            await File.WriteAllTextAsync(oldDiagnostic, "{}");
            await File.WriteAllTextAsync(unrelated, "{}");
            File.SetLastWriteTimeUtc(oldDiagnostic, DateTime.UtcNow.AddDays(-20));
            File.SetLastWriteTimeUtc(unrelated, DateTime.UtcNow.AddDays(-20));
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Diagnostics:Directory"] = folder,
                        ["ConnectionStrings:Hiring"] = connection,
                        ["Ai:ApiKey"] = "fixture-only",
                        ["Ai:Model"] = "fixture-model",
                    }
                )
                .Build();
            var auditCount = await db.Audit.CountAsync();
            await RetentionCleanup.RunAsync(db, config, CancellationToken.None);
            Check(
                await db.Revisions.CountAsync() == 1
                    && !File.Exists(oldDiagnostic)
                    && File.Exists(unrelated),
                "retention deletes only eligible old revisions and diagnostic files"
            );
            Check(
                await db.Audit.CountAsync() == auditCount
                    && await db.Opportunities.CountAsync() == 6,
                "retention preserves opportunities and admin history"
            );

            var paged = new Source
            {
                Name = "Paged fixture",
                Url = "https://example.com/pages",
                Enabled = true,
                UsageApproved = true,
                NextDueAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            };
            db.Sources.Add(paged);
            await db.SaveChangesAsync();
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton<IConfiguration>(config);
            services.AddDbContext<HiringDb>(b => b.UseSqlServer(connection));
            services.AddScoped<OpportunityService>();
            services.AddScoped<CollectionPipeline>();
            services.AddSingleton<UrlPolicy>();
            services.AddScoped<AttachmentReader>();
            services.AddScoped<BrowserCollector, PagedFixtureCollector>();
            services
                .AddHttpClient<AiExtractor>()
                .ConfigurePrimaryHttpMessageHandler(() => new FixtureResponseHandler());
            using var provider = services.BuildServiceProvider();
            await using (var scope = provider.CreateAsyncScope())
                await scope
                    .ServiceProvider.GetRequiredService<CollectionPipeline>()
                    .TickAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            var cursor = await db.DiscoveryCheckpoints.SingleAsync(x => x.SourceId == paged.Id);
            Check(
                cursor.CompletedPages == 1
                    && await db.SourceItems.AnyAsync(x =>
                        x.SourceId == paged.Id && x.ExternalId == "page-one"
                    ),
                "interrupted discovery atomically saves page items and the next cursor"
            );
            var pendingSource = await db.Sources.FindAsync(paged.Id);
            pendingSource!.CollectionRetryAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
            await using (var scope = provider.CreateAsyncScope())
                await scope
                    .ServiceProvider.GetRequiredService<CollectionPipeline>()
                    .TickAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            Check(
                !await db.DiscoveryCheckpoints.AnyAsync(x => x.SourceId == paged.Id)
                    && await db.SourceItems.CountAsync(x => x.SourceId == paged.Id) == 2,
                "resumed discovery starts at the saved page and completes without duplicate items"
            );
            Check(
                await db.AiUsage.AnyAsync(x =>
                    x.PromptVersionId == next.Id && x.Success == true && x.TotalTokens == 15
                ),
                "AI usage stores actual reported counts and prompt version"
            );
            await ToolSqlChecks.RunAsync(db, config, provider);
            Console.WriteLine(
                $"{passed} workflow SQL checks passed in an isolated temporary database."
            );
        }
        finally
        {
            if (
                db.Database.GetDbConnection().Database == name
                && name.StartsWith("HiringPs_WorkflowChecks_", StringComparison.Ordinal)
            )
                await db.Database.EnsureDeletedAsync();
            Directory.Delete(folder, recursive: true);
        }
    }

    private sealed class PagedFixtureCollector(
        UrlPolicy policy,
        IConfiguration configuration,
        AttachmentReader reader
    ) : BrowserCollector(policy, configuration, reader)
    {
        public override async Task DiscoverResumableAsync(
            Source source,
            DiscoveryCheckpoint checkpoint,
            Func<DiscoveryBatch, CancellationToken, Task> save,
            CancellationToken ct
        )
        {
            if (checkpoint.CompletedPages == 0)
            {
                await save(
                    new(
                        [("page-one", "https://example.com/one")],
                        "https://example.com/page-two",
                        1
                    ),
                    ct
                );
                throw new IOException("Simulated interruption after page commit");
            }
            if (
                checkpoint.CompletedPages != 1
                || checkpoint.NextUrl != "https://example.com/page-two"
            )
                throw new Exception("Wrong resume position");
            await save(new([("page-two", "https://example.com/two")], null, 2), ct);
        }

        public override Task<ContentBundle> FetchBundleAsync(
            Source source,
            string url,
            CancellationToken ct
        ) => Task.FromResult(new ContentBundle("Fixture role Fixture org غزة", []));
    }

    private sealed class FixtureResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        ) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"candidates":[{"content":{"parts":[{"text":"{\"inScope\":true,\"opportunities\":[{\"childKey\":\"fixture\",\"kind\":\"Job\",\"title\":\"Fixture role\",\"organization\":\"Fixture org\",\"locations\":\"غزة\",\"locationEvidence\":\"غزة\",\"summary\":\"Fixture summary\",\"originalLanguage\":\"en\",\"isInPalestine\":true}]}"}]}}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":5,"totalTokenCount":15}}""",
                        Encoding.UTF8,
                        "application/json"
                    ),
                }
            );
    }
}
