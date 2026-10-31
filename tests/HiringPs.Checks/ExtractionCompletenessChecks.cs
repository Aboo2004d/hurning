using HiringPs.Core;
using HiringPs.Web.Areas.Admin.Controllers;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Checks source-backed recovery and stable identities without contacting AI or production storage.</summary>
internal static class ExtractionCompletenessChecks
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
        var raw = new ExtractedOpportunity { ChildKey = "مساعد إداري", Kind = OpportunityKind.Job,
            Title = "مساعد إداري", Organization = "الشركة الأهلية" };
        const string source = "**موقع العمل:** الخليل - فرش الهوى - مقابل كيوي مول* **طبيعة الدوام:** دوام كامل---### **المؤهل العلمي:** شهادة الدبلوم كحد أدنى* مكان العمل: الخليل الراتب: الجنس:";
        var recovered = ExplicitExtractionRecovery.Complete(raw, null, source, true, []);
        Check(recovered.Value.Locations == "الخليل - فرش الهوى - مقابل كيوي مول", "recover full explicit work address");
        Check(recovered.Value.IsInPalestine == true && source.Contains(recovered.Value.LocationEvidence), "local scope requires source-backed evidence");
        Check(raw.Locations == "" && raw.LocationEvidence == "" && raw.IsInPalestine is null, "raw AI result remains untouched");
        Check(recovered.Value.EmploymentType.StartsWith("دوام كامل"), "recover explicit employment type");
        Check(recovered.Value.Deadline is null, "do not invent a deadline");
        Check(ExplicitExtractionRecovery.Complete(raw, null, source, false, []).Value.Locations == "", "do not share a global address among multiple roles");
        Check(ExplicitExtractionRecovery.Complete(raw, null, "مقر الشركة في الخليل", true, []).Value.Locations == "", "company headquarters alone is insufficient");
        Check(ExplicitExtractionRecovery.Complete(raw, null, "وظائف مشابهة موقع العمل: الخليل", true, []).Value.Locations == "", "exclude related job addresses");
        var conflict = ExplicitExtractionRecovery.Complete(raw, null, "موقع العمل: الخليل\nمكان العمل: نابلس", true, []);
        Check(conflict.Value.Locations == "" && conflict.Warnings.Length > 0, "conflicting addresses require review");
        var foreign = ExplicitExtractionRecovery.Complete(raw, null, "موقع العمل: القاهرة", true, []);
        Check(foreign.Value.IsInPalestine is null, "foreign address is not approved as local");
        var stored = new Opportunity { Id = 73, Kind = OpportunityKind.Job, Title = raw.Title, Organization = raw.Organization, Locations = "الخليل" };
        var retained = ExplicitExtractionRecovery.Complete(raw, stored, "نص بلا مكان عمل", true, []);
        Check(retained.Value.Locations == "الخليل" && retained.Value.LocationEvidence == "" && retained.Warnings.Length > 0, "retain previous display only under review when evidence disappears");
        var link = new OpportunitySourceLink { OpportunityId = 73, Opportunity = stored, ChildKey = "64755" };
        var consumed = new HashSet<OpportunitySourceLink>();
        Check(ExtractionRoleIdentity.Match(recovered.Value, [link], [raw], consumed) == link, "changed child key reuses unique historical role");
        consumed.Add(link);
        Check(ExtractionRoleIdentity.Match(recovered.Value, [link], [raw], consumed) is null, "a historical role cannot be consumed twice");
        var other = new OpportunitySourceLink { OpportunityId = 540, Opportunity = new Opportunity { Kind = stored.Kind, Title = stored.Title, Organization = stored.Organization, Locations = stored.Locations }, ChildKey = "other" };
        Check(ExtractionRoleIdentity.Match(recovered.Value, [link, other], [raw], new HashSet<OpportunitySourceLink>()) is null, "ambiguous historical duplicates do not auto merge");
        other.ChildKey = raw.ChildKey;
        Check(ExtractionRoleIdentity.Match(recovered.Value, [link, other], [raw], new HashSet<OpportunitySourceLink>()) == other, "exact key takes priority");
        Check(!ExplicitWorkLocationRules.Compatible("الخليل", "نابلس"), "different cities remain distinct");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation("Latin1_General_100_BIN2", string.CompareOrdinal);
        await using var db = new FixtureDb(new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection).Options);
        var tables = new[] { typeof(Opportunity), typeof(Source), typeof(SourceItem), typeof(OpportunitySourceLink),
            typeof(OpportunityDateRecord), typeof(PromptVersion), typeof(OrganizationAlias), typeof(AuditEntry),
            typeof(OpportunityPublication), typeof(IndexNowEntry), typeof(DuplicateCandidate), typeof(CategoryGroup), typeof(CategoryGroupMember), typeof(WorkflowSettings), typeof(SourceItemRevision), typeof(AiUsage), typeof(PipelineState) }
            .Select(t => db.Model.FindEntityType(t)!.GetTableName()).ToArray();
        foreach (var sql in db.Database.GenerateCreateScript().Split(';', StringSplitOptions.RemoveEmptyEntries))
            if (tables.Any(t => sql.TrimStart().StartsWith($"CREATE TABLE \"{t}\"")))
                await db.Database.ExecuteSqlRawAsync(sql);
        var item = new SourceItem { Source = new Source { Name = "fixture" }, Content = source,
            Url = "https://palcareer.com/job-single.aspx?id=64755" };
        stored.Summary = "ملخص محفوظ";
        stored.Status = PublicationStatus.NeedsReview;
        link.SourceItem = item;
        db.SourceLinks.Add(link);
        await db.SaveChangesAsync();
        var service = new OpportunityExtractionService(db);
        var result = new ExtractionResult(true, [raw]);
        var originalJson = JsonSerializer.Serialize(result, AiExtractor.JsonOptions);
        await service.ApplyExtractionAsync(item, result, default);
        Check(await db.Opportunities.CountAsync() == 1 && await db.SourceLinks.CountAsync() == 1,
            "pipeline reuses stored opportunity after key change");
        Check(link.ChildKey == "64755" && stored.Category == "إدارة" && stored.Locations == recovered.Value.Locations,
            "pipeline retains stable stored key and saves recovered address");
        Check(!item.Error.Contains("disappeared") && item.ExtractionJson == originalJson,
            "pipeline avoids false disappearance and retains raw response");
        await service.ApplyExtractionAsync(item, result, default);
        Check(await db.Opportunities.CountAsync() == 1, "repeated extraction is idempotent");
        var duplicateRecord = new Opportunity { Kind = stored.Kind, Title = stored.Title,
            Organization = stored.Organization, Locations = stored.Locations, Status = PublicationStatus.NeedsReview };
        db.SourceLinks.Add(new OpportunitySourceLink { SourceItem = item, Opportunity = duplicateRecord, ChildKey = raw.ChildKey });
        await db.SaveChangesAsync();
        await service.ApplyExtractionAsync(item, result, default);
        Check(await db.Opportunities.CountAsync() == 2 && item.Error.Contains("Multiple stored records")
            && !item.Error.Contains("disappeared"), "existing duplicate histories require review without creating another record or false disappearance");
        Check(stored.Status == PublicationStatus.NeedsReview && duplicateRecord.Status == PublicationStatus.NeedsReview,
            "existing duplicates stay in review instead of automatically merging");
        duplicateRecord.AdminOverride = true;
        duplicateRecord.Locations = "مكان معتمد يدويًا";
        await db.SaveChangesAsync();
        await service.ApplyExtractionAsync(item, result, default);
        Check(duplicateRecord.Locations == "مكان معتمد يدويًا", "manual override remains protected");
        var custom = new CategoryGroup { Name = "مجموعة معتمدة سابقة", NormalizedName = "custom" };
        custom.Members.Add(new CategoryGroupMember { Label = "الهندسة المدنية", NormalizedKey = CategoryGroupMap.Key("الهندسة المدنية") });
        db.CategoryGroups.Add(custom);
        await db.SaveChangesAsync();
        var reader = new CategoryGroupReadService(db);
        var groupsController = new CategoryGroupsController(reader, new CategoryGroupSuggestionService(db), new CategoryGroupManagementService(db));
        var foundGroup = await reader.FindAsync(custom.Id, default);
        Check(foundGroup?.Id == custom.Id && foundGroup.Name == custom.Name
            && foundGroup.Members.SequenceEqual(["الهندسة المدنية"]), "group editor lookup filters by entity ID and returns its members");
        Check(await reader.FindAsync(int.MaxValue, default) is null, "missing group lookup returns null");
        var edit = (ViewResult)await groupsController.Edit(custom.Id, null, default);
        Check(edit.ViewName == "Editor" && ((CategoryGroupInput)edit.Model!).Id == custom.Id,
            "reported category group edit route opens without a translation exception");
        Check(await groupsController.Edit(int.MaxValue, null, default) is NotFoundResult,
            "deleted group edit route returns not found");
        var proposal = (ViewResult)await groupsController.ProposeFamily("engineering", default);
        var proposalInput = (CategoryGroupInput)proposal.Model!;
        Check(proposalInput.Name == "الهندسة" && proposalInput.Members.Contains("الهندسة الكهربائية")
            && !proposalInput.Members.Contains("الهندسة المدنية"), "family draft includes canonical specialties without stealing an approved membership");
        Check(await db.CategoryGroups.CountAsync() == 1, "opening a family draft does not write or approve groups");
        Check(await groupsController.ProposeFamily("invalid", default) is NotFoundResult, "unknown taxonomy family is rejected");
        Check((await new CategoryCatalogService(db).OptionsAsync(default)).Contains("الهندسة الكهربائية"), "canonical fields are selectable before their first opportunity");
        var historicalPrompt = PromptDefaults.Initial();
        db.PromptVersions.Add(historicalPrompt);
        db.WorkflowSettings.Add(new WorkflowSettings { ActivePromptVersionId = historicalPrompt.Id });
        await db.SaveChangesAsync();
        var promptController = new AiPromptController(db, new UrlPolicy(), new ConfigurationBuilder().Build(), new PromptAdministrationService(db))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var promptDraft = (ViewResult)await promptController.CategoryPolicy(default);
        var promptInput = (PromptEditor)promptDraft.Model!;
        Check(promptDraft.ViewName == "AiPrompt" && promptInput.Template.Contains(PromptDefaults.CategoryPolicyMarker),
            "AI policy action prepares a versioned draft through the existing prompt editor");
        Check(await db.PromptVersions.CountAsync() == 1 && (await db.WorkflowSettings.SingleAsync()).PendingPromptVersionId is null
            && !historicalPrompt.Template.Contains(PromptDefaults.CategoryPolicyMarker), "opening category policy draft does not alter or activate a saved prompt");
        // Run the actual processing service: stale paid results must not bypass the new
        // contract, while valid unchanged results still avoid another paid request.
        var processingConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Ai:ApiKey"] = "fixture", ["Ai:Model"] = "fixture-model", ["Ai:ExtractionRepairAttempts"] = "0", ["Ai:DailyTokenBudget"] = "0", ["Ai:SaveRejectedResponses"] = "false"
        }).Build();
        var dbOptions = new DbContextOptionsBuilder<HiringDb>().UseSqlite(connection).Options;
        using var scopes = new ServiceCollection().AddScoped<HiringDb>(_ => new FixtureDb(dbOptions)).BuildServiceProvider();
        var completeSource = "مساعد إداري الشركة الأهلية " + source;
        await using var browser = new ProcessingBrowser(processingConfig) { Content = completeSource };
        var good = new ExtractedOpportunity { ChildKey = "assistant", Kind = OpportunityKind.Job, Title = "مساعد إداري",
            Organization = "الشركة الأهلية", Locations = "الخليل - فرش الهوى - مقابل كيوي مول", LocationEvidence = "الخليل - فرش الهوى - مقابل كيوي مول",
            Category = "إدارة", EmploymentType = "دوام كامل", Summary = "مساعد إداري للعمل في الشركة", IsInPalestine = true };
        var handler = new ProcessingReply { Extraction = new ExtractionResult(true, [good]) };
        using var http = new HttpClient(handler);
        var extractor = new AiExtractor(http, processingConfig, scopes.GetRequiredService<IServiceScopeFactory>());
        var pipeline = new SourceItemProcessingService(db, browser, extractor, new OpportunityService(db), processingConfig, NullLogger<CollectionPipeline>.Instance);
        var processingItem = new SourceItem { Source = new Source { Name = "processing fixture", Url = "https://example.test/list" },
            ExternalId = "processing", Url = "https://example.test/job", Content = completeSource, ContentHash = ContentFingerprint.Hash(completeSource),
            ExtractionJson = originalJson, ExtractionPromptVersionId = historicalPrompt.Id, Status = ProcessingStatus.Discovered };
        db.SourceItems.Add(processingItem);
        await db.SaveChangesAsync();
        await pipeline.ProcessAsync(processingItem.Id, historicalPrompt, null, _ => Task.CompletedTask,
            (_, _) => throw new Exception("Budget is disabled in this isolated fixture"), default);
        Check(handler.Calls == 1 && browser.Fetches == 1 && processingItem.ExtractionJson.Contains("assistant"),
            "invalid unchanged cache causes a fresh provider call rather than another incomplete application");
        Check((await db.AiUsage.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync()).Success == true,
            "complete provider response records successful usage");
        processingItem.Status = ProcessingStatus.Discovered;
        await db.SaveChangesAsync();
        await pipeline.ProcessAsync(processingItem.Id, historicalPrompt, null, _ => Task.CompletedTask,
            (_, _) => throw new Exception("Budget is disabled"), default);
        Check(handler.Calls == 1 && browser.Fetches == 2, "valid unchanged cache is still reused without extra AI cost");
        var rejectedItem = new SourceItem { Source = processingItem.Source, ExternalId = "rejected", Url = "https://example.test/rejected",
            Content = completeSource, ContentHash = ContentFingerprint.Hash(completeSource), Status = ProcessingStatus.Discovered };
        db.SourceItems.Add(rejectedItem);
        await db.SaveChangesAsync();
        good.WorkMode = "On-site / duties and qualifications copied into the wrong field";
        await pipeline.ProcessAsync(rejectedItem.Id, historicalPrompt, null, _ => Task.CompletedTask,
            (_, _) => throw new Exception("Budget is disabled"), default);
        var rejectedSaved = await db.SourceItems.AsNoTracking().SingleAsync(x => x.Id == rejectedItem.Id);
        Check(rejectedSaved.Status == ProcessingStatus.NeedsReview && rejectedSaved.Error.Contains("workMode") && rejectedSaved.ExtractionJson == "",
            "bad provider result leaves a specific source review reason and is never cached as a successful extraction");
        Check((await db.AiUsage.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync()).Success == false
            && (await db.AiUsage.AsNoTracking().OrderByDescending(x => x.Id).FirstAsync()).ErrorType == nameof(AiExtractionRejectedException),
            "HTTP 200 with mixed fields records failed extraction usage");
        var rssItem = new SourceItem { Source = new Source { Name = "RSS fixture", Url = OpportunityRss.JobsUrl, Connector = ConnectorKind.JobsRss },
            ExternalId = "rss", Url = "https://www.jobs.ps/jobs/fixture-123", Content = "Data Entry Operator Workplace: ...", Status = ProcessingStatus.AwaitingAi };
        db.SourceItems.Add(rssItem);
        await db.SaveChangesAsync();
        browser.Content = "Data Entry Operator MSF Workplace: Gaza Responsibilities: enter approved project data into records accurately and maintain confidentiality.";
        handler.Extraction = new ExtractionResult(true, [new ExtractedOpportunity { ChildKey = "entry", Kind = OpportunityKind.Job, Title = "Data Entry Operator", Organization = "MSF",
            OriginalLanguage = "en", Summary = "Enter project records.", Locations = "Gaza", LocationEvidence = "Workplace: Gaza", IsInPalestine = true, Category = "إدارة" }]);
        var beforeFetch = browser.Fetches;
        await pipeline.ProcessAsync(rssItem.Id, historicalPrompt, null, _ => Task.CompletedTask,
            (_, _) => throw new Exception("Budget is disabled"), default);
        var rssSaved = await db.SourceItems.AsNoTracking().SingleAsync(x => x.Id == rssItem.Id);
        Check(browser.Fetches == beforeFetch + 1 && rssSaved.Content.Contains("Workplace: Gaza") && rssSaved.ExtractionJson.Contains("entry"),
            "legacy RSS summary already awaiting AI is refetched before a new extraction");
        // A corrective attempt has its own usage row and effective prompt hash.
        good.WorkMode = "";
        var invalidRepair = JsonSerializer.Deserialize<ExtractedOpportunity>(JsonSerializer.Serialize(good, AiExtractor.JsonOptions), AiExtractor.JsonOptions)!;
        invalidRepair.WorkMode = "On-site / copied duties and conditions";
        var correctionItem = new SourceItem { SourceId = processingItem.SourceId, ExternalId = "correction", Url = "https://example.test/correction",
            Content = completeSource, ContentHash = ContentFingerprint.Hash(completeSource), Status = ProcessingStatus.AwaitingAi };
        db.SourceItems.Add(correctionItem);
        await db.SaveChangesAsync();
        processingConfig["Ai:ExtractionRepairAttempts"] = "1";
        var firstCorrectionCall = handler.Calls + 1;
        handler.Next = call => new ExtractionResult(true, [call == firstCorrectionCall ? invalidRepair : good]);
        await pipeline.ProcessAsync(correctionItem.Id, historicalPrompt, null, _ => Task.CompletedTask,
            (_, _) => throw new Exception("Budget is disabled"), default);
        var attempts = await db.AiUsage.AsNoTracking().Where(x => x.SourceItemId == correctionItem.Id).OrderBy(x => x.Id).ToListAsync();
        Check(attempts.Count == 2 && attempts[0].Success == false && attempts[1].Success == true
            && attempts[0].PromptHash != attempts[1].PromptHash, "correction attempts record separate outcomes, tokens and actual instruction hashes");
        handler.Next = _ => new ExtractionResult(true, [invalidRepair]);
        var pauseState = new PipelineState { Id = 1, AiPausedUntil = DateTimeOffset.UtcNow.AddHours(1) };
        db.PipelineStates.Add(pauseState);
        await db.SaveChangesAsync();
        var beforePause = handler.Calls;
        try { await extractor.ExtractAsync(completeSource, default, new AiExecution(historicalPrompt, correctionItem.Id)); throw new Exception("Expected quality rejection under pause"); }
        catch (AiExtractionRejectedException) { Check(handler.Calls == beforePause + 1, "global pause prevents an additional corrective request"); }
        pauseState.AiPausedUntil = null;
        await db.SaveChangesAsync();
        processingConfig["Ai:DailyTokenBudget"] = "1";
        var beforeBudget = handler.Calls;
        try { await extractor.ExtractAsync(completeSource, default, new AiExecution(historicalPrompt, correctionItem.Id)); throw new Exception("Expected quality rejection at budget"); }
        catch (AiExtractionRejectedException) { Check(handler.Calls == beforeBudget + 1, "spent token budget prevents an additional corrective request"); }
        Console.WriteLine($"Extraction completeness checks passed: {count}");
    }
    private sealed class ProcessingBrowser(IConfiguration config) : BrowserCollector(null!, config, null!)
    {
        public string Content { get; set; } = "";
        public int Fetches { get; private set; }
        public override Task<ContentBundle> FetchBundleAsync(Source source, string url, CancellationToken ct)
        { Fetches++; return Task.FromResult(new ContentBundle(Content, [])); }
    }
    private sealed class ProcessingReply : HttpMessageHandler
    {
        public ExtractionResult Extraction { get; set; } = new(false, []);
        public int Calls { get; private set; }
        public Func<int, ExtractionResult>? Next { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var json = JsonSerializer.Serialize(Next?.Invoke(Calls) ?? Extraction, AiExtractor.JsonOptions);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text = json } } } } },
                usageMetadata = new { promptTokenCount = 10, candidatesTokenCount = 5, totalTokenCount = 15 }
            })) });
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
                    if (p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?))
                        p.SetValueConverter(new DateTimeOffsetToBinaryConverter());
                    if (p.Name == "Version" && p.ClrType == typeof(byte[])) p.ValueGenerated = ValueGenerated.Never;
                }
        }
    }
}
