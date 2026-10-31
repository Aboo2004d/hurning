using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HiringPs.Core;
using Microsoft.Extensions.Configuration;

/// <summary>Exercises actual provider parsing and extraction quality with fixture HTTP, never a live AI call.</summary>
internal static class AiExtractionQualityChecks
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
        var directory = Path.Combine(Path.GetTempPath(), "hiring-ai-quality-" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Ai:ApiKey"] = "fixture-not-a-real-key", ["Ai:Model"] = "fixture-model", ["Ai:ExtractionRepairAttempts"] = "0", ["Diagnostics:Directory"] = directory
        }).Build();
        const string source = "Senior Staff Nurse Duty Station: HEBRON Posting Period: 14 October 2026-27 October 2026 Job Opening number: 26-MED-286027. Responsibilities: provide nursing care.";
        ExtractedOpportunity Good() => new() { ChildKey = "26-MED-286027", Kind = OpportunityKind.Job, Title = "Senior Staff Nurse", Organization = "UNRWA",
            Locations = "HEBRON", LocationEvidence = "Duty Station: HEBRON", IsInPalestine = true, Category = "التمريض", OriginalLanguage = "en", Summary = "Nursing role." };
        JsonObject Json(ExtractedOpportunity role) => JsonSerializer.SerializeToNode(new ExtractionResult(true, [role]), AiExtractor.JsonOptions)!.AsObject();
        async Task<ExtractionResult> Extract(JsonObject json)
        {
            using var client = new HttpClient(new Handler(json.ToJsonString()));
            return await new AiExtractor(client, configuration).ExtractAsync(source, default);
        }
        async Task Reject(JsonObject json, string phrase)
        {
            try { await Extract(json); throw new Exception("Expected extraction rejection: " + phrase); }
            catch (AiExtractionRejectedException ex) { Check(ex.Message.Contains(phrase) && ex.DiagnosticId is not null, "reject and diagnose " + phrase); }
        }
        try
        {
            var schema = JsonSerializer.SerializeToElement(GeminiClient.ResponseSchema);
            var item = schema.GetProperty("properties").GetProperty("opportunities").GetProperty("items");
            Check(item.GetProperty("properties").EnumerateObject().Select(x => x.Name).SequenceEqual(AiExtractionContract.Fields), "provider properties and DTO field names cannot drift apart");
            var required = item.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToHashSet();
            Check(AiExtractionContract.Fields.All(x => required.Contains(x)) && required.Count == AiExtractionContract.Fields.Length, "provider schema requires every extraction field");
            Check(item.GetProperty("propertyOrdering").EnumerateArray().Select(x => x.GetString()).SequenceEqual(AiExtractionContract.Fields), "provider field order follows the prompt schema");
            Check(item.GetProperty("properties").GetProperty("workMode").GetProperty("enum").GetArrayLength() == 4, "work mode is a constrained short classification");
            Check(item.GetProperty("properties").EnumerateObject().All(x => x.Value.TryGetProperty("description", out _)), "every provider field has its own semantic description");
            Check((await Extract(Json(Good()))).Opportunities[0].Locations == "HEBRON", "complete supported response passes actual HTTP parsing");
            var missing = Json(Good()); missing["opportunities"]![0]!.AsObject().Remove("locations");
            await Reject(missing, "omitted fields");
            var nullText = Json(Good()); nullText["opportunities"]![0]!["locations"] = null;
            await Reject(nullText, "empty string");
            var role = Good(); role.WorkMode = "On-site / " + new string('x', 46_804);
            await Reject(Json(role), "workMode");
            role = Good(); role.Locations = ""; role.LocationEvidence = ""; role.IsInPalestine = null;
            await Reject(Json(role), "work location");
            role = Good(); role.Category = "";
            await Reject(Json(role), "professional category");
            role = Good(); role.Kind = OpportunityKind.Tender; role.WorkMode = "On-site";
            await Reject(Json(role), "non-job");
            var duplicated = Json(Good()); var second = JsonSerializer.SerializeToNode(Good(), AiExtractor.JsonOptions)!;
            second["childKey"] = "another-key"; duplicated["opportunities"]!.AsArray().Add(second);
            await Reject(duplicated, "repeated role");
            role = Good(); role.Summary = new string('x', 300);
            Check((await Extract(Json(role))).Opportunities[0].Summary.Length == 300, "summary target remains a writing instruction rather than a hard database cap");
            Check(AiExtractionContract.CanReuse(Json(Good()).ToJsonString(), source), "complete cached extraction can be reused");
            Check(!AiExtractionContract.CanReuse(missing.ToJsonString(), source), "missing-field cache must be re-extracted");
            role = Good(); role.WorkMode = "Permanent contract with many qualifications";
            Check(!AiExtractionContract.CanReuse(Json(role).ToJsonString(), source), "mixed-field cache must be re-extracted");
            var absentDeadline = new ExtractionResult(true, [Good()]);
            Check(AiExtractionContract.Issues(absentDeadline, source).Count == 0, "genuinely absent deadline does not require review");
            Check(AiExtractionContract.Issues(absentDeadline, source + " application deadline: 19/11/2026").Any(x => x.Contains("deadline")), "explicit dated closing clause cannot be silently omitted");
            var nurseRecovery = ExplicitExtractionRecovery.Complete(new ExtractedOpportunity { Kind = OpportunityKind.Job, Title = "Senior Staff Nurse" }, null, source, true, []);
            Check(nurseRecovery.Value.Locations == "HEBRON" && nurseRecovery.Value.LocationEvidence == "HEBRON", "flat UNRWA location stops before Posting Period");
            const string area = "مكان العمل: رام الله ومنطقة الوسط.طريقة التقديم: إرسال السيرة الذاتية. معلومات الوظيفة مكان العمل: رام الله والبيرة الراتب: الجنس:";
            var areaRecovery = ExplicitExtractionRecovery.Complete(new ExtractedOpportunity { Kind = OpportunityKind.Job, Title = "مندوب مبيعات" }, null, area, true, []);
            Check(areaRecovery.Value.Locations.Length > 0 && !areaRecovery.Warnings.Any(x => x.Contains("Conflicting")), "exported Ramallah area descriptions do not create a false location conflict");
            Check(!ExplicitWorkLocationRules.ConsistentScope("الخليل - فرش الهوى", "الخليل - البلدة القديمة"), "different specific street addresses still require review");
            Check(!ExplicitWorkLocationRules.ConsistentScope("رام الله", "نابلس"), "different cities remain conflicting");
            Check(!ExplicitWorkLocationRules.ConsistentScope("رام الله", "رام الله - القاهرة"), "explicit foreign contradiction cannot be approved through containment");
            var institution = ExplicitExtractionRecovery.Complete(new ExtractedOpportunity { Kind = OpportunityKind.Job, Title = "مهندس حاسوب" }, null,
                "مكان العمل: وزارة الزراعة (1) وديوان الرقابة (2) الاختصاص العام للوظيفة: إدارة نظم التشغيل مكان العمل: رام الله والبيرة الراتب:", true, []);
            Check(institution.Value.Locations == "رام الله والبيرة" && institution.Value.Category == "تكنولوجيا المعلومات", "institution allocation does not erase an explicit geographic field");
            Check(CategoryTaxonomy.Canonical("الصيانة") == "المهن الفنية" && CategoryTaxonomy.Canonical("الخدمات") == "خدمات عامة"
                && CategoryTaxonomy.Canonical("النقل والمواصلات") == "النقل والتوزيع", "exported legacy category aliases retain their canonical meaning");
            Check(CategoryTaxonomy.Classify(new() { Title = "Data Entry Operator" }).Category == "إدارة"
                && CategoryTaxonomy.Classify(new() { Title = "Education Specialist (School Health)" }).Category == "التعليم", "exported specialist titles classify by actual role");
            const string graze = "الرئيسية الوظائف اتصل بنا موظف موظف موظف رام الله والبيرة تفاصيل الوظيفة مطلوب موظف للعمل في محل Graze للأجبان .. الشركة: محل Graze مكان العمل: رام الله والبيرة";
            var oldRole = new Opportunity { Id = 441, Kind = OpportunityKind.Job, Title = "موظف", Organization = "محل Graze", Locations = "رام الله والبيرة" };
            var incoming = new ExtractedOpportunity { ChildKey = "مطلوب موظف للعمل في محل Graze للأجبان", Kind = OpportunityKind.Job, Title = "مطلوب موظف للعمل في محل Graze للأجبان", Organization = "محل Graze للأجبان", Locations = "رام الله والبيرة" };
            var oldLink = new OpportunitySourceLink { ChildKey = "64773", Opportunity = oldRole, OpportunityId = oldRole.Id };
            Check(ExtractionRoleIdentity.Match(incoming, [oldLink], [incoming], new HashSet<OpportunitySourceLink>(), graze) == oldLink,
                "source-supported title expansion reuses the unique old role instead of declaring disappearance");
            Check(ExtractionRoleIdentity.Match(incoming, [oldLink], [incoming, Good()], new HashSet<OpportunitySourceLink>(), graze) is null,
                "generic-heading fallback is forbidden for a multi-role extraction");
            var conflicting = new ExtractedOpportunity { ChildKey = "x", Kind = OpportunityKind.Job, Title = incoming.Title, Organization = incoming.Organization, ReferenceNumber = "new-ref" };
            oldRole.ReferenceNumber = "old-ref";
            Check(ExtractionRoleIdentity.Match(conflicting, [oldLink], [conflicting], new HashSet<OpportunitySourceLink>(), graze) is null, "explicit reference differences block title-expansion identity");
            var boolAsText = Json(Good()); boolAsText["opportunities"]![0]!["isInPalestine"] = "true";
            Check(!AiExtractionContract.CanReuse(boolAsText.ToJsonString(), source), "string booleans cannot masquerade as cached facts");
            oldRole.ReferenceNumber = "same-ref";
            var renamed = Good(); renamed.Organization = oldRole.Organization; renamed.Title = "عنوان وظيفي محدث"; renamed.ReferenceNumber = "same-ref";
            Check(ExtractionRoleIdentity.Match(renamed, [oldLink], [renamed], new HashSet<OpportunitySourceLink>()) == oldLink,
                "unique explicit reference preserves identity when wording changes materially");
            var repairConfig = new ConfigurationBuilder().AddConfiguration(configuration).AddInMemoryCollection(new Dictionary<string, string?> {
                ["Ai:ExtractionRepairAttempts"] = "1", ["Ai:SaveRejectedResponses"] = "false"
            }).Build();
            using (var repairHandler = new SequenceHandler(missing.ToJsonString(), Json(Good()).ToJsonString()))
            using (var repairClient = new HttpClient(repairHandler))
            {
                var repaired = await new AiExtractor(repairClient, repairConfig).ExtractAsync(source, default);
                Check(repairHandler.Calls == 2 && repaired.Opportunities[0].Locations == "HEBRON"
                    && repairHandler.Instructions.Contains("EXTRACTION CORRECTION v1"), "one corrective request repairs an incomplete response");
            }
            using (var failureHandler = new SequenceHandler(missing.ToJsonString()))
            using (var failureClient = new HttpClient(failureHandler))
            {
                try { await new AiExtractor(failureClient, repairConfig).ExtractAsync(source, default); throw new Exception("Expected rejection after correction"); }
                catch (AiExtractionRejectedException) { Check(failureHandler.Calls == 2, "a failed correction never creates an unbounded paid retry loop"); }
            }
            var diagnostics = Directory.GetFiles(Path.Combine(directory, "ai-responses"), "*.json");
            Check(diagnostics.Length == 7 && diagnostics.All(p => File.ReadAllText(p).Contains("providerResponse") && !File.ReadAllText(p).Contains("fixture-not-a-real-key")), "rejected provider envelopes are retained without API headers or credentials");
            Check(RssJobPage.IsSummary("Data Entry Operator Workplace: ... SOURCE LINKS: https://www.jobs.ps/jobs/data-entry-69685"), "exported truncated RSS entry requires original detail");
            var page = RssJobPage.ContentHtml("<html><nav>Other jobs</nav><main><div class='job-description'><h1>Data Entry Operator</h1><p>Workplace: Gaza</p><p>Responsibilities: maintain the project records and enter approved data accurately into the database.</p></div><aside class='related-jobs'>Foreign vacancy in Cairo</aside></main></html>");
            Check(page.Contains("Workplace: Gaza") && !page.Contains("Cairo") && !page.Contains("Other jobs"), "original page extraction excludes related listings and navigation");
            var parsedPage = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(page);
            Check(SourceFieldQuotes.Locations(SourceHtmlText.Read(parsedPage)).SequenceEqual(["Gaza"]), "HTML field boundaries survive adjacent paragraph tags");
            var jobHeader = RssJobPage.ContentHtml("<header>Global navigation</header><main><article><header><h1>Nurse</h1><p>Workplace: Gaza</p></header><p>Responsibilities: provide nursing care, support patient follow-up and maintain accurate confidential medical records.</p></article></main>");
            Check(jobHeader.Contains("Nurse") && !jobHeader.Contains("Global navigation"), "actual job header survives removal of the site header");
            var feed = System.Xml.Linq.XDocument.Parse("<rss><channel><item><title>Job</title><link>https://www.jobs.ps/jobs/fixture-123</link><description>Short teaser...</description></item></channel></rss>");
            Check(OpportunityRss.Parse(feed, ConnectorKind.JobsRss).Count == 1, "short feed descriptions still discover original job URLs");
            try { RssJobPage.ContentHtml("<main><p>Workplace: ... This is a brief introduction to an organization and its ongoing humanitarian activities...</p></main>"); throw new Exception("Expected summary rejection"); }
            catch (ContentNeedsReviewException) { Check(true, "truncated original page cannot fall back to a feed summary"); }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine($"AI extraction quality: {passed} checks passed without external requests.");
    }
    private sealed class SequenceHandler(params string[] replies) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string Instructions { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Instructions = body.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString()!;
            var reply = replies[Math.Min(Calls++, replies.Length - 1)];
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new {
                candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text = reply } } } } }
            })) };
        }
    }
    private sealed class Handler(string extraction) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(JsonSerializer.Serialize(new { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text = extraction } } } } } }), Encoding.UTF8, "application/json")
        });
    }
}
