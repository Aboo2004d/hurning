using HiringPs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

public static class SocialTemplateChecks
{
    public static async Task RunAsync()
    {
        void Check(bool value, string label)
        {
            if (!value)
                throw new Exception("FAIL: " + label);
            Console.WriteLine("PASS: " + label);
        }
        var item = new Opportunity
        {
            Title = "مهندس",
            Organization = "جهة",
            Summary = "ملخص",
            Kind = OpportunityKind.Job,
            OriginalLanguage = "ar",
        };
        Check(
            SocialTemplateText.Valid(SocialTemplateText.DefaultBody),
            "Default template uses supported variables"
        );
        Check(
            !SocialTemplateText.Valid("{{unknown}}")
                && !SocialTemplateText.Valid("{{title")
                && !SocialTemplateText.Valid(new string('a', 50001)),
            "Unknown, malformed and oversized templates are rejected"
        );
        var rendered = SocialTemplateText.Render(SocialTemplateText.DefaultBody, item);
        Check(
            rendered.Contains("مهندس")
                && rendered.Contains("وظيفة")
                && !rendered.Contains("آخر موعد")
                && !rendered.Contains("📍"),
            "Missing deadline and location remove their labels"
        );
        item.Deadline = new DateOnly(2027, 1, 2);
        item.Locations = "غزة";
        Check(
            SocialTemplateText.Render(
                "{{title}} | {{organization}}\n{{location}}\n{{deadline}}",
                item
            ) == "مهندس | جهة\nغزة\n2027-01-02",
            "Literal text and multiple variables render with exact source values"
        );
        item.Kind = (OpportunityKind)999;
        Check(
            SocialTemplateText.Render("{{kind}}", item) == "فرصة",
            "Future opportunity kind renders a general label"
        );
        item.Title = "{{deadline}} <script>";
        Check(
            SocialTemplateText.Render("{{title}}", item) == item.Title,
            "Source values are not recursively interpreted as template syntax"
        );
        using var db = new DesignTimeFactory().CreateDbContext([]);
        Check(!db.Database.HasPendingModelChanges(), "Template migration snapshot matches model");
        var model = db.GetService<IDesignTimeModel>().Model;
        Check(
            model
                .FindEntityType(typeof(SocialTemplateVersion))!
                .GetIndexes()
                .Any(x => x.IsUnique && x.GetFilter() == "[Active] = 1"),
            "Database permits only one active version per platform and scope"
        );
        Check(
            model
                .FindEntityType(typeof(SocialPublication))!
                .GetForeignKeys()
                .Any(x => x.Properties.Any(p => p.Name == "TemplateVersionId")),
            "Publications retain their template version by foreign key"
        );
        var version = new SocialTemplateVersion
        {
            Id = 42,
            Body = "old",
            Provider = "Facebook",
            Scope = "general",
        };
        db.Attach(version);
        version.Body = "changed";
        try
        {
            await db.SaveChangesAsync();
            throw new Exception("Immutable version was accepted");
        }
        catch (InvalidOperationException ex)
            when (ex.Message.StartsWith("Social template versions are immutable"))
        {
            Console.WriteLine("PASS: Existing version text cannot be overwritten");
        }
    }

    public static async Task SqlAsync(DbContextOptions<HiringDb> options)
    {
        await using var db = new HiringDb(options);
        if (await db.SocialTemplateVersions.CountAsync() != 10)
            throw new Exception("Missing initial template versions");
        var templates = new SocialTemplates(db);
        var general = await templates.ResolveAsync("Facebook", (OpportunityKind)999, default);
        if (general.Scope != "general" || general.Provider != "Facebook")
            throw new Exception("General fallback failed");
        var job = await templates.ResolveAsync("Facebook", OpportunityKind.Job, default);
        if (job.Scope != "Job")
            throw new Exception("Specific template did not win");
        var linked = await templates.ResolveAsync("LinkedIn", (OpportunityKind)999, default);
        if (linked.Id == general.Id || linked.Provider != "LinkedIn")
            throw new Exception("Platforms share fallback");
        async Task SaveVersion(long expected, string body, long? restore = null)
        {
            await using var context = new HiringDb(options);
            var configuration =
                new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
            var publishing = new SocialPublishing(
                context,
                configuration,
                new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(),
                new NoHttpFactory()
            );
            var controller = new HiringPs.Web.Areas.Admin.Controllers.SocialTemplatesController(
                context,
                publishing
            );
            var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = http,
            };
            controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                http,
                new EmptyTempData()
            );
            await controller.Save(
                new HiringPs.Web.Areas.Admin.Controllers.SocialTemplateInput
                {
                    Provider = "Facebook",
                    Scope = "Job",
                    Body = body,
                    ExpectedActiveId = expected,
                },
                restore,
                default
            );
        }
        await SaveVersion(job.Id, "New {{title}}");
        var second = await templates.ResolveAsync("Facebook", OpportunityKind.Job, default);
        if (second.Number != 2 || second.Body != "New {{title}}")
            throw new Exception("New version not activated");
        await SaveVersion(job.Id, "Stale {{title}}");
        if (
            (await templates.ResolveAsync("Facebook", OpportunityKind.Job, default)).Id != second.Id
        )
            throw new Exception("Stale edit overwrote active version");
        await SaveVersion(second.Id, "", job.Id);
        var restored = await templates.ResolveAsync("Facebook", OpportunityKind.Job, default);
        if (restored.Number != 3 || restored.Id == job.Id || restored.Body != job.Body)
            throw new Exception("Restore did not create a new immutable version");
        if (
            await db.SocialTemplateVersions.CountAsync(x =>
                x.Provider == "Facebook" && x.Scope == "Job" && x.Active
            ) != 1
        )
            throw new Exception("Multiple active versions");
        Console.WriteLine(
            "PASS: SQL save, stale edit rejection and restore preserve history and one active version"
        );
        Console.WriteLine(
            "PASS: SQL migration seeds 10 versions; specific and future-kind fallback resolve per platform"
        );
    }

    private sealed class NoHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new Exception("Template operations must not contact providers");
    }

    private sealed class EmptyTempData : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(
            Microsoft.AspNetCore.Http.HttpContext context
        ) => new Dictionary<string, object>();

        public void SaveTempData(
            Microsoft.AspNetCore.Http.HttpContext context,
            IDictionary<string, object> values
        ) { }
    }
}
