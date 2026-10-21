using System.Net;
using System.Text;
using System.Text.Json;
using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

public static class WorkflowChecks
{
    public static async Task RunAsync()
    {
        var passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new Exception(name);
            passed++;
            Console.WriteLine("PASS " + name);
        }
        using var modelDb = new HiringDb(
            new DbContextOptionsBuilder<HiringDb>()
                .UseSqlServer(
                    "Server=(localdb)\\MSSQLLocalDB;Database=ModelCheck;Trusted_Connection=True"
                )
                .Options
        );
        Check(
            !modelDb.Database.HasPendingModelChanges(),
            "current model matches migrations without connecting to SQL Server"
        );
        var upgradeSql = modelDb.GetService<IMigrator>().GenerateScript(
            "20261021120000_ConsolidateUnusedDefaultPrompt", "20261021140000_InitializeCurrentDefaultPrompt"
        );
        string FrozenLiteral(string name)
        {
            var match = System.Text.RegularExpressions.Regex.Match(upgradeSql,
                "DECLARE @" + name + " nvarchar\\(max\\) = N'((?:''|[^'])*)';",
                System.Text.RegularExpressions.RegexOptions.Singleline, TimeSpan.FromSeconds(1));
            Check(match.Success, "frozen migration contains " + name);
            return match.Groups[1].Value.Replace("''", "'");
        }
        Check(
            FrozenLiteral("template") == PromptDefaults.CurrentTemplate.ReplaceLineEndings("\n")
                && FrozenLiteral("dates") == PromptDatePolicy.CurrentDateContract,
            "final initialization freezes the exact latest default title and date instructions"
        );
        Check(
            upgradeSql.Contains("IF NOT EXISTS (SELECT 1 FROM [PromptVersions] WITH (UPDLOCK, HOLDLOCK))")
                && upgradeSql.Contains("SET IDENTITY_INSERT [PromptVersions] ON")
                && upgradeSql.Contains("VALUES (1, 1, NULL)")
                && !upgradeSql.Contains("DELETE FROM [PromptVersions]")
                && !upgradeSql.Contains("UPDATE [PromptVersions]"),
            "single-base initialization preserves existing admin and usage history"
        );
        var completeSql = modelDb.GetService<IMigrator>().GenerateScript();
        var previousSql = modelDb.GetService<IMigrator>().GenerateScript(
            toMigration: "20261021120000_ConsolidateUnusedDefaultPrompt");
        Check(
            !previousSql.Contains("INSERT INTO [PromptVersions]")
                && !previousSql.Contains("INSERT INTO [WorkflowSettings]")
                && !previousSql.Contains("You extract Palestinian opportunities")
                && System.Text.RegularExpressions.Regex.Matches(completeSql,
                    @"INSERT INTO \[PromptVersions\]").Count == 1
                && System.Text.RegularExpressions.Regex.Matches(completeSql,
                    @"INSERT INTO \[WorkflowSettings\]").Count == 1,
            "full migration chain initializes only the latest base prompt, never an intermediate old version"
        );
        var prompt = PromptDefaults.Initial();
        var currentDefault = PromptDefaults.Current();
        Check(
            currentDefault.Render().Contains(PromptDefaults.TitleInstructions)
                && currentDefault.Render().Contains(PromptDefaults.CurrentDateInstructions)
                && !currentDefault.Render().Contains("{{scope}}"),
            "current default includes original-role titles, optional closing and opening-date separation"
        );
        Check(
            !prompt.Template.Contains("TITLE POLICY v1")
                && !prompt.Template.Contains("DEFAULT DATE POLICY v1"),
            "new default does not rewrite historical migration seed"
        );
        Check(
            new PromptVersion { FinalText = "Saved custom prompt" }.Render() == "Saved custom prompt"
                && !new PromptVersion { Template = "Custom extraction instructions" }.Compose().Contains("TITLE POLICY v1"),
            "saved admin instructions take precedence over current default policies"
        );
        Check(
            !prompt.LegacySnapshot.Contains('\r')
                && !prompt.Template.Contains('\r')
                && !prompt.FinalText.Contains('\r'),
            "initial prompt seed uses consistent LF line endings on every platform"
        );
        Check(
            TextRules.Hash(prompt.LegacySnapshot)
                == "BB53571E5A8B143C2AEF99C016604110E6C655FC2E480C9B3E9A66F28090F169",
            "initial prompt keeps its complete historical snapshot"
        );
        prompt.DateContract = PromptDatePolicy.CurrentDateContract;
        prompt.FinalText = ""; // Editing a copy creates a new snapshot, never mutates a saved version.
        prompt.SummaryLimit = 150;
        prompt.AllowedKinds = "Job,Training";
        prompt.AllowUnknownDeadline = false;
        var rendered = prompt.Render();
        Check(
            rendered.Contains("at most 150 characters") && !rendered.Contains("{{summaryLimit}}"),
            "summary conditions replace placeholders"
        );
        Check(
            rendered.Contains("Allowed opportunity kinds: Job,Training")
                && rendered.Contains("unspecified closing date requires review"),
            "system conditions enter the final prompt"
        );
        Check(
            prompt.LegacySnapshot == PromptDefaults.Legacy.ReplaceLineEndings("\n"),
            "editing conditions cannot rewrite the legacy snapshot"
        );
        Check(
            PromptWorkflow.Protected(
                new() { Status = PublicationStatus.Published, PromptVersionId = 1 },
                2
            ),
            "published data is protected from a changed prompt"
        );
        Check(
            PromptWorkflow.Protected(new() { Status = PublicationStatus.Archived }, 2),
            "legacy archived data is protected"
        );
        Check(
            PromptWorkflow.Protected(new() { AdminOverride = true }, 2),
            "manual decisions remain protected"
        );
        Check(
            !PromptWorkflow.Protected(
                new() { Status = PublicationStatus.NeedsReview, PromptVersionId = 1 },
                2
            ),
            "unpublished review data can be reprocessed"
        );
        Check(
            !PromptWorkflow.Protected(
                new() { Status = PublicationStatus.Published, PromptVersionId = 1 },
                1
            ),
            "normal source updates are distinct from prompt reprocessing"
        );

        CheckedDates Dates(string content, ExtractedOpportunity value) =>
            DateEvidenceRules.Check(value, content);
        Check(
            new PromptVersion().DateContract.Contains("SEMANTIC DEADLINE POLICY v1"),
            "new prompt versions include semantic closing-date instructions"
        );
        Check(
            !PromptDefaults.Initial().DateContract.Contains("SEMANTIC DEADLINE POLICY v1"),
            "historical migration prompt remains unchanged"
        );
        var date = new DateOnly(2026, 8, 16);
        foreach (
            var quote in new[]
            {
                "لن تقبل طلبات الالتحاق بعد يوم 2026/10/19.",
                "تستمر فرصة إرسال طلبك حتى 19 أكتوبر 2026.",
                "Applications will no longer be accepted after October 19, 2026.",
                "La réception des candidatures prend fin le 2026/10/19.",
            }
        )
        {
            var checkedDate = Dates(
                quote,
                new() { Deadline = new(2026, 10, 19), DeadlineEvidence = quote }
            );
            Check(
                checkedDate.Errors.Count == 0 && checkedDate.Dates.Count == 1,
                "semantic closing classification is not restricted to known keywords: " + quote
            );
        }
        const string registrationClosing = "نهاية التسجيل تكون نهاية يوم الاثنين 2026/10/19.";
        var registrationDate = new DateOnly(2026, 10, 19);
        var registrationCheck = Dates(
            registrationClosing,
            new() { Deadline = registrationDate, DeadlineEvidence = registrationClosing }
        );
        Check(
            registrationCheck.Errors.Count == 0 && registrationCheck.Dates.Count == 1,
            "registration closing quotation with weekday and year-first slash date is verified"
        );
        foreach (
            var label in new[]
            {
                "انتهاء التسجيل",
                "إغلاق التسجيل",
                "نهاية التقديم",
                "نهاية استقبال الطلبات",
            }
        )
        {
            var quote = label + ": 2026/10/19";
            Check(
                Dates(
                    quote,
                    new() { Deadline = registrationDate, DeadlineEvidence = quote }
                ).Errors.Count == 0,
                "explicit application closing wording is recognized: " + label
            );
        }
        Check(
            Dates(
                registrationClosing,
                new()
                {
                    Deadline = registrationDate.AddDays(1),
                    DeadlineEvidence = registrationClosing,
                }
            ).Errors.Count > 0,
            "recognized registration ending still rejects a different extracted date"
        );
        Check(
            Dates(
                "نص آخر لا يحتوي الاقتباس",
                new() { Deadline = registrationDate, DeadlineEvidence = registrationClosing }
            ).Errors.Count > 0,
            "registration deadline still requires the quotation in the original source"
        );
        foreach (var quote in new[] { "نهاية التدريب 2026/10/19", "بداية التسجيل 2026/10/19" })
            Check(
                Dates(
                    quote,
                    new() { Deadline = registrationDate, DeadlineEvidence = quote }
                ).Errors.Count > 0,
                "training ending or registration opening cannot become an application deadline"
            );

        Check(
            OpportunityService.DateEvidenceMatches("لتقديم16/08/2026", date),
            "numeric date adjacent to Arabic is recognized"
        );
        Check(
            !OpportunityService.DateEvidenceMatches("116/08/2026", date),
            "a date substring inside a larger number is rejected"
        );
        Check(
            !OpportunityService.DateEvidenceMatches("31/02/2026", new(2026, 2, 28)),
            "an invalid calendar date is rejected"
        );
        Check(
            Dates(
                "آخر موعد للتقديم16/08/2026",
                new() { Deadline = date, DeadlineEvidence = "آخر موعد للتقديم16/08/2026" }
            ).Dates.Count == 1,
            "labeled Arabic closing date is verified"
        );
        Check(
            Dates(
                "تاريخ النشر: 16/08/2026",
                new() { Deadline = date, DeadlineEvidence = "تاريخ النشر: 16/08/2026" }
            ).Errors.Count > 0,
            "publication date cannot become a deadline"
        );
        Check(
            Dates(
                "آخر تحديث: 16/08/2026",
                new() { Deadline = date, DeadlineEvidence = "آخر تحديث: 16/08/2026" }
            ).Errors.Count > 0,
            "modification date cannot become a deadline"
        );
        Check(
            Dates(
                "فتح المظاريف: 16/08/2026",
                new() { Deadline = date, DeadlineEvidence = "فتح المظاريف: 16/08/2026" }
            ).Errors.Count > 0,
            "bid opening cannot become the submission deadline"
        );
        Check(
            Dates(
                "Closing date: 22 September 2026",
                new()
                {
                    Deadline = new(2026, 9, 22),
                    DeadlineEvidence = "Closing date: 22 September 2026",
                }
            ).Errors.Count == 0,
            "English month names are verified with their role"
        );
        Check(
            Dates(
                "آخر موعد للتقديم 30 سبتمبر 2026",
                new()
                {
                    Deadline = new(2026, 9, 30),
                    DeadlineEvidence = "آخر موعد للتقديم 30 سبتمبر 2026",
                }
            ).Errors.Count == 0,
            "Arabic month names are verified with their role"
        );
        Check(
            Dates(
                "تاريخ النشر: 16/08/2026",
                new() { PublicationDate = date, PublicationEvidence = "تاريخ النشر: 16/08/2026" }
            )
                .Dates.Single()
                .Kind == OpportunityDateKind.Publication,
            "publication date keeps its source event type"
        );
        Check(
            Dates(
                "آخر تحديث: 16/08/2026",
                new() { ModificationDate = date, ModificationEvidence = "آخر تحديث: 16/08/2026" }
            )
                .Dates.Single()
                .Kind == OpportunityDateKind.Modification,
            "modification date keeps its source event type"
        );
        Check(
            Dates(
                "زيارة الموقع: 16/08/2026",
                new()
                {
                    OtherDates =
                    [
                        new()
                        {
                            Kind = OpportunityDateKind.SiteVisit,
                            Date = date,
                            Evidence = "زيارة الموقع: 16/08/2026",
                        },
                    ],
                }
            ).Errors.Count == 0,
            "site visit is a separate source event"
        );
        Check(
            Dates(
                "شراء كراسة العطاء: 16/08/2026",
                new()
                {
                    OtherDates =
                    [
                        new()
                        {
                            Kind = OpportunityDateKind.BookletPurchase,
                            Date = date,
                            Evidence = "شراء كراسة العطاء: 16/08/2026",
                        },
                    ],
                }
            ).Errors.Count == 0,
            "booklet purchase is a separate source event"
        );
        Check(
            Dates(
                "تم تمديد آخر موعد للتقديم إلى 16/08/2026",
                new()
                {
                    Deadline = date,
                    DeadlineIsExtension = true,
                    DeadlineEvidence = "تم تمديد آخر موعد للتقديم إلى 16/08/2026",
                }
            ).Errors.Count == 0,
            "an extension needs an explicit source statement"
        );
        Check(
            Dates(
                "آخر موعد للتقديم: 16/08/2026",
                new()
                {
                    Deadline = date,
                    DeadlineIsExtension = true,
                    DeadlineEvidence = "آخر موعد للتقديم: 16/08/2026",
                }
            ).Errors.Count > 0,
            "an unsupported extension claim is rejected"
        );
        Check(
            Dates(
                "آخر موعد للتقديم: 16/08/2026",
                new()
                {
                    Deadline = new(2026, 8, 17),
                    DeadlineEvidence = "آخر موعد للتقديم: 16/08/2026",
                }
            ).Errors.Count > 0,
            "a different numeric date is rejected"
        );
        Check(
            Dates(
                "آخر موعد للتقديم: 16/08/2026",
                new() { Deadline = date, DeadlineEvidence = "Closing date: 16/08/2026" }
            ).Errors.Count > 0,
            "translated or invented evidence is rejected"
        );
        Check(
            Dates("لا يوجد موعد محدد", new()).Errors.Count == 0,
            "an absent date is not invented"
        );

        foreach (
            var (location, query) in new[]
            {
                ("رام الله", "Ramallah"),
                ("Jericho", "أريحا"),
                ("الخليل", "Hebron"),
                ("Tulkarem", "طولكرم"),
                ("East Jerusalem", "القدس"),
                ("بيت لحم", "Bethlehem"),
            }
        )
            Check(
                LocationSearch.Index(location).Contains(LocationSearch.Normalize(query)),
                "location alias: " + query
            );
        Check(
            LocationSearch.Normalize("أَرِيحا") == LocationSearch.Normalize("اريحا"),
            "search removes Arabic formatting differences"
        );
        var opportunity = new Opportunity
        {
            Summary = new string('a', 250),
            Title = "role",
            Organization = "org",
            Locations = "غزة",
        };
        Check(
            !OpportunityService.Validate(opportunity).Any(x => x.Contains("200")),
            "summary length remains prompt-only"
        );
        var telemetry = new AiTelemetry();
        using (
            var payload = JsonDocument.Parse(
                """{"usageMetadata":{"promptTokenCount":123,"candidatesTokenCount":45,"totalTokenCount":180}}"""
            )
        )
            telemetry.Read(payload.RootElement);
        Check(
            telemetry.Input == 123
                && telemetry.Output == 45
                && telemetry.Total == 180
                && telemetry.Cached is null,
            "usage keeps reported counts and unknown values distinct"
        );
        var missing = new AiTelemetry();
        using (var payload = JsonDocument.Parse("{}"))
            missing.Read(payload.RootElement);
        Check(
            missing.Total is null && missing.Input is null,
            "missing token metadata is not fabricated as zero"
        );
        var handler = new FixtureHandler();
        using var client = new HttpClient(handler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Ai:ApiKey"] = "fixture-not-a-real-key",
                    ["Ai:Model"] = "fixture-model",
                }
            )
            .Build();
        var result = await new AiExtractor(client, config).ExtractAsync(
            "source fixture",
            CancellationToken.None,
            new AiExecution(prompt, null)
        );
        Check(
            result.PromptVersionId == 1 && result.Opportunities.Count == 0,
            "extractor associates the chosen prompt version with its parsed response"
        );
        Check(
            handler.Instructions.Contains("at most 150 characters")
                && handler.Instructions.Contains("DATE CONTRACT")
                && handler.Instructions.Contains("SEMANTIC DEADLINE POLICY v1"),
            "HTTP payload uses the rendered version and typed date instructions"
        );
        Console.WriteLine(
            $"{passed} workflow checks passed; no external HTTP requests or application database writes."
        );
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        public string Instructions { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Instructions = body
                .RootElement.GetProperty("systemInstruction")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString()!;
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"candidates":[{"content":{"parts":[{"text":"{\"inScope\":true,\"opportunities\":[]}"}]}}],"usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":5,"totalTokenCount":15}}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        }
    }
}
