namespace HiringPs.Core;

/// <summary>Visible run metrics only, ordered by run id in the dashboard query.</summary>
public sealed record ScrapeRunSummaryDto(
    long Id,
    int? SourceId,
    DateTimeOffset StartedAt,
    string Status,
    int Found,
    int Changed,
    string Error
);
