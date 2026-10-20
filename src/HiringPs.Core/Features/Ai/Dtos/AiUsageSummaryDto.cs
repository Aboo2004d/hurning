namespace HiringPs.Core;

/// <summary>Visible AI invocation fields without the unused request metadata.</summary>
public sealed record AiUsageSummaryDto(
    long Id,
    long? SourceItemId,
    int PromptVersionId,
    string Model,
    bool? Success,
    string ErrorType,
    int? HttpStatus,
    int? TotalTokens,
    long DurationMs,
    DateTimeOffset StartedAt
);
