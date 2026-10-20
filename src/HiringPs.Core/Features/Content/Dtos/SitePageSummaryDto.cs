namespace HiringPs.Core;

/// <summary>Managed page list metadata without page bodies or concurrency state.</summary>
public sealed record SitePageSummaryDto(
    string Key,
    DateTimeOffset UpdatedAt
);
