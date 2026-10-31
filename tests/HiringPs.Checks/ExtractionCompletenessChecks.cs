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
            typeof(OpportunityPublication), typeof(IndexNowEntry), typeof(DuplicateCandidate), typeof(CategoryGroup), typeof(CategoryGroupMember), typeof(WorkflowSettings) }
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
        Console.WriteLine($"Extraction completeness checks passed: {count}");
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
