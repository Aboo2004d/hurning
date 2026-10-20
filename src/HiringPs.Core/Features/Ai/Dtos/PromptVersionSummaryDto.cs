namespace HiringPs.Core;

/// <summary>Prompt history fields; the preserved legacy snapshot remains visible to administrators.</summary>
public sealed record PromptVersionSummaryDto(
    int Id,
    DateTimeOffset CreatedAt,
    string Note,
    string FinalText,
    string LegacySnapshot
);
