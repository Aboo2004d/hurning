namespace HiringPs.Core;

/// <summary>Merge list fields; excludes restoration snapshots and concurrency tokens.</summary>
public sealed record MergeOperationSummaryDto(
    long Id,
    long DuplicateId,
    long TargetId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UndoneAt
);
