using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

public sealed class SocialTemplateVersion
{
    public long Id { get; set; }

    [MaxLength(20)]
    public string Provider { get; set; } = "";

    // A string scope allows future opportunity kinds without changing the schema.
    [MaxLength(60)]
    public string Scope { get; set; } = "general";
    public int Number { get; set; }
    public bool Active { get; set; }

    [MaxLength(50000)]
    public string Body { get; set; } = "";

    [MaxLength(256)]
    public string Actor { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class SocialTemplateText
{
    public const string DefaultBody =
        "📢 {{kind}}\n\n💼 {{title}}\n🏢 {{organization}}\n📍 {{location}}\n📅 آخر موعد للتقديم: {{deadline}}\n\n{{summary}}\n\nللتفاصيل وطريقة التقديم 👇";
    public static readonly string[] Tokens =
    [
        "kind",
        "title",
        "organization",
        "location",
        "deadline",
        "summary",
    ];
    private static readonly Regex Placeholder = new(
        @"\{\{\s*([^{}]+?)\s*\}\}",
        RegexOptions.CultureInvariant
    );

    public static bool Valid(string body) =>
        !string.IsNullOrWhiteSpace(body)
        && body.Length <= 50000
        && Placeholder.Matches(body).All(m => Tokens.Contains(m.Groups[1].Value))
        && !Placeholder.Replace(body, "").Contains("{{")
        && !Placeholder.Replace(body, "").Contains("}}");

    public static string Render(string body, Opportunity item)
    {
        var english = item.OriginalLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var kind = item.Kind switch
        {
            OpportunityKind.Job => english ? "Job" : "وظيفة",
            OpportunityKind.Training => english ? "Training" : "تدريب",
            OpportunityKind.Tender => english ? "Tender" : "عطاء",
            OpportunityKind.ProcurementNotice => english ? "Procurement notice" : "مناقصة",
            _ => english ? "Opportunity" : "فرصة",
        };
        var values = new Dictionary<string, string>
        {
            ["kind"] = kind,
            ["title"] = item.Title,
            ["organization"] = item.Organization,
            ["location"] = item.Locations,
            ["deadline"] = item.Deadline?.ToString("yyyy-MM-dd") ?? "",
            ["summary"] = item.Summary,
        };
        // Work line by line so an unknown deadline never leaves a dangling label.
        return string.Join(
                "\n",
                body.Replace("\r\n", "\n")
                    .Split('\n')
                    .Where(line =>
                        !Placeholder
                            .Matches(line)
                            .Any(m =>
                                string.IsNullOrWhiteSpace(
                                    values.GetValueOrDefault(m.Groups[1].Value)
                                )
                            )
                    )
                    .Select(line =>
                        Placeholder.Replace(
                            line,
                            m => values.GetValueOrDefault(m.Groups[1].Value) ?? ""
                        )
                    )
            )
            .Trim();
    }
}

public sealed class SocialTemplates(HiringDb db)
{
    public async Task<SocialTemplateVersion> ResolveAsync(
        string provider,
        OpportunityKind kind,
        CancellationToken ct
    )
    {
        var scope = kind.ToString();
        return await db
                .SocialTemplateVersions.AsNoTracking()
                .Where(x =>
                    x.Provider == provider && x.Active && (x.Scope == scope || x.Scope == "general")
                )
                .OrderByDescending(x => x.Scope == scope)
                .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("SocialTemplateMissing");
    }
}
