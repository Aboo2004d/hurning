namespace HiringPs.Core;

/// <summary>Editable fields copied from one saved AI prompt version.</summary>
public sealed record PromptEditDto(
    int Id,
    string Template,
    int SummaryLimit,
    string GeographicScope,
    string AllowedKinds,
    bool AllowUnknownDeadline
);
