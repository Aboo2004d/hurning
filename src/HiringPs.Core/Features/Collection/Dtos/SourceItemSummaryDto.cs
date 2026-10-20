namespace HiringPs.Core;

/// <summary>Queue row without source configuration, full content or extraction JSON.</summary>
public sealed record SourceItemSummaryDto(
    long Id,
    string SourceName,
    string ExternalId,
    ProcessingStatus Status,
    string Error
);
