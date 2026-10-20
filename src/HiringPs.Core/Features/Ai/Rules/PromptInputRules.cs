using System.Text.RegularExpressions;

namespace HiringPs.Core;

/// <summary>Pure validation rules for versioned prompt inputs; no version is saved or activated here.</summary>
public static class PromptInputRules
{
    /// <summary>Normalizes comma-separated kind names, preserving case-sensitive persisted enum names.</summary>
    public static string[] NormalizeKinds(string? kinds) => (kinds ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct()
        .ToArray();

    /// <summary>Rejects empty lists, numeric enum values and unknown or differently cased kind names.</summary>
    public static bool ValidKinds(string[] kinds) => kinds.Length > 0 && kinds.All(value =>
        Enum.TryParse<OpportunityKind>(value, out var kind) && Enum.IsDefined(kind) && kind.ToString() == value);

    /// <summary>Allows only the four template variables understood by prompt composition.</summary>
    public static bool HasUnknownVariables(string? template)
    {
        string[] allowed = ["summaryLimit", "scope", "allowedKinds", "allowUnknownDeadline"];
        return Regex.Matches(template ?? "", @"\{\{(.*?)\}\}")
            .Any(match => !allowed.Contains(match.Groups[1].Value));
    }
}
