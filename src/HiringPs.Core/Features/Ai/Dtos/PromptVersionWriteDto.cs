namespace HiringPs.Core;

/// <summary>Validated new prompt version request and workflow rowversion; does not activate it immediately.</summary>
public sealed record PromptVersionWriteDto(
    string Template,
    int SummaryLimit,
    string GeographicScope,
    string AllowedKinds,
    bool AllowUnknownDeadline,
    string Note,
    byte[] Version
);
