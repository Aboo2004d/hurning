namespace HiringPs.Web.Infrastructure;

public static class AdminActionRoutes
{
    public static readonly IReadOnlyDictionary<string, string> Controllers = new Dictionary<
        string,
        string
    >(StringComparer.OrdinalIgnoreCase)
    {
        ["Index"] = "Admin",
        ["Pages"] = "Pages",
        ["PageContent"] = "Pages",
        ["Diagnostic"] = "Diagnostics",
        ["DiagnosticImage"] = "Diagnostics",
        ["Opportunities"] = "Opportunities",
        ["Edit"] = "Opportunities",
        ["Merge"] = "Opportunities",
        ["Messages"] = "Messages",
        ["Message"] = "Messages",
        ["MessageStatus"] = "Messages",
        ["Sources"] = "Sources",
        ["Source"] = "Sources",
        ["CollectNow"] = "Sources",
        ["SuggestSource"] = "Sources",
        ["Item"] = "Items",
        ["Retry"] = "Items",
        ["Reports"] = "Reports",
        ["Resolve"] = "Reports",
        ["AiPrompt"] = "AiPrompt",
        ["PreviewPrompt"] = "AiPrompt",
        ["AiUsage"] = "AiUsage",
        ["Retention"] = "Retention",
        ["Merges"] = "Merges",
        ["UndoMerge"] = "Merges",
        ["Tools"] = "Tools",
        ["CheckTools"] = "Tools",
        ["CheckAi"] = "Tools",
        ["SiteQuestions"] = "SiteQuestions",
    };
}
